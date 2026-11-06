using System.Buffers;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static BraviaRemote.GoogleTvWire;

namespace BraviaRemote;

public sealed record GoogleTvPairing(string Fingerprint, string Certificate);

public static class GoogleTvApps
{
    private const uint Ping = 1;
    private const uint Power = 32;
    private const uint AppLinks = 512;

    public static async Task<GoogleTvPairing?> PairAsync(
        IPAddress address, Func<bool, Task<string?>> readCode)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN=BRAVIA Remote {Guid.NewGuid():N}", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
        var certificateBytes = generated.Export(X509ContentType.Pkcs12, "");
        using var certificate = X509CertificateLoader.LoadPkcs12(certificateBytes, "");
        using var client = new TcpClient(address.AddressFamily);
        using var handshake = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await client.ConnectAsync(address, 6467, handshake.Token);
        using var tls = new SslStream(client.GetStream(), false, (_, peer, _, _) => peer is not null);
        await AuthenticateAsync(tls, address, certificate, handshake.Token);
        using var server = X509CertificateLoader.LoadCertificate(tls.RemoteCertificate!.GetRawCertData());

        await PairMessageAsync(tls, 10, Message(Text(1, "atvremote"), Text(2, "BRAVIA Remote")), 11, handshake.Token);
        var encoding = Message(Number(1, 3), Number(2, 6));
        await PairMessageAsync(tls, 20, Message(Bytes(1, encoding), Number(3, 1)), 20, handshake.Token);
        await PairMessageAsync(tls, 30, Message(Bytes(1, encoding), Number(2, 1)), 31, handshake.Token);

        var incorrect = false;
        while (true)
        {
            var code = await readCode(incorrect);
            if (code is null)
                return null;

            var secret = GetSecret(certificate, server, code.Trim());
            if (secret is null)
            {
                incorrect = true;
                continue;
            }

            using var confirmation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await PairMessageAsync(tls, 40, Bytes(1, secret), 41, confirmation.Token);
            return new(Convert.ToHexString(server.GetCertHash(HashAlgorithmName.SHA256)), Convert.ToBase64String(certificateBytes));
        }
    }

    public static async Task LaunchAsync(IPAddress address, GoogleTvPairing pairing, string package)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var token = timeout.Token;
        using var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(pairing.Certificate), "");
        using var client = new TcpClient(address.AddressFamily);
        await client.ConnectAsync(address, 6466, token);
        using var tls = new SslStream(client.GetStream(), false, (_, peer, _, _) =>
            peer is not null && string.Equals(pairing.Fingerprint,
                Convert.ToHexString(peer.GetCertHash(HashAlgorithmName.SHA256)), StringComparison.Ordinal));
        await AuthenticateAsync(tls, address, certificate, token);

        uint features = 0;
        while (true)
        {
            var message = await ReadRemoteAsync(tls, token);
            var configuration = message.FirstOrDefault(field => field.Number == 1).Data;
            if (configuration is not null)
            {
                var offered = ReadFields(configuration).FirstOrDefault(field => field.Number == 1).Value;
                features = offered & (AppLinks | Ping | Power);
                if ((features & AppLinks) == 0)
                    throw new IOException("The TV does not support app links.");
                var info = Message(Number(3, 1), Text(4, "1"), Text(5, "BRAVIA Remote"), Text(6, "1.0"));
                await SendAsync(tls, Bytes(1, Message(Number(1, features), Bytes(2, info))), token);
            }

            if (features != 0 && message.Any(field => field.Number == 2))
            {
                await SendAsync(tls, Bytes(2, Number(1, features)), token);
                break;
            }
        }

        var launch = package == "com.google.android.youtube.tv"
            ? Text(1, "vnd.youtube://")
            : Message(Text(1, $"https://play.google.com/apps/launch?id={Uri.EscapeDataString(package)}"),
                Text(2, "com.android.vending"), Number(3, 1));
        await SendAsync(tls, Bytes(90, launch), token);

        // There is no positive launch response. Listen briefly for rejection or a ping.
        using var response = CancellationTokenSource.CreateLinkedTokenSource(token);
        response.CancelAfter(TimeSpan.FromMilliseconds(850));
        try
        {
            while (true)
                await ReadRemoteAsync(tls, response.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
        }
    }

    private static async Task<List<Field>> ReadRemoteAsync(SslStream tls, CancellationToken token)
    {
        var fields = ReadFields(await ReceiveAsync(tls, token));
        if (fields.Any(field => field.Number == 3))
            throw new IOException("The TV rejected a Google TV message.");
        var ping = fields.FirstOrDefault(field => field.Number == 8).Data;
        if (ping is not null)
        {
            var sequence = ReadFields(ping).FirstOrDefault(field => field.Number == 1).Value;
            await SendAsync(tls, Bytes(9, Number(1, sequence)), token);
        }
        return fields;
    }

    private static Task AuthenticateAsync(SslStream stream, IPAddress address, X509Certificate2 certificate, CancellationToken token) =>
        stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = address.ToString(),
            ClientCertificates = new X509CertificateCollection { certificate },
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        }, token);

    private static async Task PairMessageAsync(SslStream stream, int field, byte[] content, int expected, CancellationToken token)
    {
        await SendAsync(stream, Message(Number(1, 2), Number(2, 200), Bytes(field, content)), token);
        var reply = ReadFields(await ReceiveAsync(stream, token));
        if (!reply.Any(item => item.Number == 2 && item.Value == 200) || !reply.Any(item => item.Number == expected))
            throw new IOException("Google TV rejected pairing.");
    }

    private static byte[]? GetSecret(X509Certificate2 client, X509Certificate2 server, string code)
    {
        Span<byte> pin = stackalloc byte[3];
        if (code.Length != 6 || Convert.FromHexString(code.AsSpan(), pin, out _, out _) != OperationStatus.Done)
            return null;

        using var clientKey = client.GetRSAPublicKey();
        using var serverKey = server.GetRSAPublicKey();
        if (clientKey is null || serverKey is null)
            throw new IOException("Google TV did not provide an RSA pairing certificate.");

        var own = clientKey.ExportParameters(false);
        var peer = serverKey.ExportParameters(false);
        var hash = SHA256.HashData(Message(own.Modulus!, own.Exponent!, peer.Modulus!, peer.Exponent!, pin[1..].ToArray()));
        return hash[0] == pin[0] ? hash : null;
    }
}
