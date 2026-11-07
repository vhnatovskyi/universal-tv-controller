using System.Buffers;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static UniversalTvRemote.GoogleTvWire;

namespace UniversalTvRemote;

public sealed record GoogleTvPairing(string Fingerprint, string Certificate);
public sealed class AppLaunchUnconfirmedException() : IOException("The TV did not confirm the requested app in the foreground.");
public sealed class TvTextFieldRequiredException() : IOException("Open a text field on the TV before typing.");
public sealed class RemoteCommandRejectedException(uint code) : IOException($"The TV rejected the remote command ({code}).");
public sealed class TvPowerUnconfirmedException(bool? expectedOn = null) : IOException("The TV did not confirm the requested power state.")
{
    public bool? ExpectedOn { get; } = expectedOn;
}

public static partial class GoogleTvApps
{
    private const uint Ping = 1;
    private const uint Power = 32;
    private const uint AppLinks = 512;
    private const uint CurrentApp = 4;
    private const uint Keys = 2;
    private const uint Volume = 64;
    // Sony requires feature 16 for system key injection, even though volume works without it.
    private const uint SystemControl = 16;

    public static async Task TypeTextAsync(IPAddress address, GoogleTvPairing pairing, string text)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = timeout.Token;
        using var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(pairing.Certificate), "");
        using var client = new TcpClient(address.AddressFamily);
        await client.ConnectAsync(address, 6466, token);
        using var tls = new SslStream(client.GetStream(), false, (_, peer, _, _) =>
            peer is not null && string.Equals(pairing.Fingerprint,
                Convert.ToHexString(peer.GetCertHash(HashAlgorithmName.SHA256)), StringComparison.Ordinal));
        await AuthenticateAsync(tls, address, certificate, token);
        uint imeCounter = 0, fieldCounter = 0, start = 0, end = 0;
        var hasField = false;
        await ConfigureAsync(tls, Ping | Power | CurrentApp, CurrentApp, token, message =>
        {
            var editorData = message.FirstOrDefault(field => field.Number == 20).Data;
            if (editorData is not null)
            {
                var editor = ReadFields(editorData);
                var infoData = editor.FirstOrDefault(field => field.Number == 1).Data;
                if (infoData is not null)
                {
                    var info = ReadFields(infoData);
                    hasField = info.FirstOrDefault(field => field.Number == 2).Value != 0;
                    fieldCounter = info.FirstOrDefault(field => field.Number == 1).Value;
                }
                var statusData = editor.FirstOrDefault(field => field.Number == 2).Data;
                if (statusData is not null)
                {
                    var status = ReadFields(statusData);
                    imeCounter = status.FirstOrDefault(field => field.Number == 1).Value;
                    start = status.FirstOrDefault(field => field.Number == 3).Value;
                    end = status.FirstOrDefault(field => field.Number == 4).Value;
                }
            }
            var batchData = message.FirstOrDefault(field => field.Number == 21).Data;
            if (batchData is not null)
            {
                var batch = ReadFields(batchData);
                imeCounter = batch.FirstOrDefault(field => field.Number == 1).Value;
                fieldCounter = batch.FirstOrDefault(field => field.Number == 2).Value;
            }
        });
        if (!hasField)
            throw new TvTextFieldRequiredException();
        var edit = Message(Number(1, 1), Bytes(2, Message(Number(1, start), Number(2, end), Text(3, text))));
        await SendAsync(tls, Bytes(21, Message(Number(1, imeCounter), Number(2, fieldCounter), Bytes(3, edit))), token);
        await Task.Delay(250, token);
    }

    public static async Task<int> ControlAsync(IPAddress address, GoogleTvPairing pairing, int? sonyKey = null, bool? power = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = timeout.Token;
        using var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(pairing.Certificate), "");
        using var client = new TcpClient(address.AddressFamily);
        await client.ConnectAsync(address, 6466, token);
        using var tls = new SslStream(client.GetStream(), false, (_, peer, _, _) =>
            peer is not null && string.Equals(pairing.Fingerprint,
                Convert.ToHexString(peer.GetCertHash(HashAlgorithmName.SHA256)), StringComparison.Ordinal));
        await AuthenticateAsync(tls, address, certificate, token);
        int? volume = null;
        bool? screenOn = null;
        var required = sonyKey is null && power is null ? Volume : Keys | Power;
        await ConfigureAsync(tls, Ping | Keys | Power | Volume | CurrentApp | AppLinks, required, token, message =>
        {
            volume = ReadVolume(message) ?? volume;
            screenOn = ReadPower(message) ?? screenOn;
        });
        if (sonyKey is null && power is null)
        {
            while (volume is null)
                volume = ReadVolume(await ReadRemoteAsync(tls, token));
            return volume.Value;
        }
        if (power is not null && screenOn == power)
            return volume ?? 0;
        var key = power switch
        {
            // Google TV supports POWER; SLEEP/WAKEUP are ignored on some TVs.
            true or false => 26u,
            _ => sonyKey switch
            {
                0 => 26u, 1 => 178u, 6 => 3u, 8 => 4u, 9 => 19u, 10 => 20u, 11 => 22u, 12 => 21u,
                13 => 23u, 30 => 24u, 31 => 25u, 32 => 164u,
                101 => 243u, 102 => 244u, 103 => 245u, 104 => 246u,
                _ => throw new IOException("Unsupported remote key.")
            }
        };
        await SendAsync(tls, Bytes(10, Message(Number(1, key), Number(2, 3))), token);
        if (key == 26)
        {
            var expectedPower = power ?? !screenOn;
            using var confirmation = CancellationTokenSource.CreateLinkedTokenSource(token);
            confirmation.CancelAfter(TimeSpan.FromSeconds(4));
            try
            {
                while (true)
                {
                    var message = await ReadRemoteAsync(tls, confirmation.Token);
                    if (ReadPower(message) is { } observed && (expectedPower is null || observed == expectedPower))
                        return volume ?? 0;
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                throw new TvPowerUnconfirmedException(expectedPower);
            }
        }
        await Task.Delay(120, token);
        return volume ?? 0;
    }

    private static int? ReadVolume(List<Field> fields)
    {
        var data = fields.FirstOrDefault(field => field.Number == 50).Data;
        return data is null ? null : (int)ReadFields(data).FirstOrDefault(field => field.Number == 7).Value;
    }

    private static bool? ReadPower(List<Field> fields)
    {
        var data = fields.FirstOrDefault(field => field.Number == 40).Data;
        return data is null ? null : ReadFields(data).FirstOrDefault(field => field.Number == 1).Value != 0;
    }

    public static async Task<GoogleTvPairing?> PairAsync(
        IPAddress address, Func<bool, Task<string?>> readCode)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN=Universal TV Controller {Guid.NewGuid():N}", rsa,
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

        await PairMessageAsync(tls, 10, Message(Text(1, "atvremote"), Text(2, "Universal TV Controller")), 11, handshake.Token);
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
        foreach (var useLegacyLink in new[] { false, true })
        {
            try
            {
                if (await LaunchAttemptAsync(address, pairing, package, useLegacyLink))
                    return;
            }
            catch (AppLaunchUnconfirmedException)
            {
                // Older services and Play Store versions accept different launch URIs.
            }
            catch (RemoteCommandRejectedException)
            {
                // Try the other URI format when the service explicitly rejects this one.
            }
        }
        throw new AppLaunchUnconfirmedException();
    }

    private static async Task<bool> LaunchAttemptAsync(
        IPAddress address, GoogleTvPairing pairing, string package, bool useLegacyLink)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var token = timeout.Token;
        using var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(pairing.Certificate), "");
        using var client = new TcpClient(address.AddressFamily);
        await client.ConnectAsync(address, 6466, token);
        using var tls = new SslStream(client.GetStream(), false, (_, peer, _, _) =>
            peer is not null && string.Equals(pairing.Fingerprint,
                Convert.ToHexString(peer.GetCertHash(HashAlgorithmName.SHA256)), StringComparison.Ordinal));
        await AuthenticateAsync(tls, address, certificate, token);

        await ConfigureAsync(tls, AppLinks | Ping | Power | CurrentApp, AppLinks, token);

        var launch = package switch
        {
            "com.google.android.youtube.tv" => Text(1, "vnd.youtube://"),
            "com.netflix.ninja" => Text(1, "nflx://www.netflix.com/browse"),
            "com.kyivstar.tv.androidtv" => Text(1, "com.kyivstar.tv.androidtv://"),
            _ => useLegacyLink
                ? Text(1, $"market://launch?id={Uri.EscapeDataString(package)}")
                : Text(1, $"https://play.google.com/apps/launch?id={Uri.EscapeDataString(package)}")
        };
        await SendAsync(tls, Bytes(90, launch), token);

        // Confirm the foreground package, rather than treating a successful write as a launch.
        using var response = CancellationTokenSource.CreateLinkedTokenSource(token);
        response.CancelAfter(TimeSpan.FromSeconds(4));
        try
        {
            while (true)
            {
                var message = await ReadRemoteAsync(tls, response.Token);
                if (ReadActiveApp(message) == package)
                    return true;
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return false;
        }
    }

    public static async Task WatchAppAsync(IPAddress address, GoogleTvPairing pairing,
        Action<string> appChanged, Action<int> volumeChanged, Action<bool> powerChanged, CancellationToken token)
    {
        using var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(pairing.Certificate), "");
        using var client = new TcpClient(address.AddressFamily);
        using var handshake = CancellationTokenSource.CreateLinkedTokenSource(token);
        handshake.CancelAfter(TimeSpan.FromSeconds(8));
        await client.ConnectAsync(address, 6466, handshake.Token);
        using var tls = new SslStream(client.GetStream(), false, (_, peer, _, _) =>
            peer is not null && string.Equals(pairing.Fingerprint,
                Convert.ToHexString(peer.GetCertHash(HashAlgorithmName.SHA256)), StringComparison.Ordinal));
        await AuthenticateAsync(tls, address, certificate, handshake.Token);
        var initialApp = await ConfigureAsync(tls, Ping | Power | CurrentApp | Volume, CurrentApp, handshake.Token, message =>
        {
            if (ReadVolume(message) is { } volume)
                volumeChanged(volume);
            if (ReadPower(message) is { } power)
                powerChanged(power);
        });
        if (initialApp is not null)
            appChanged(initialApp);
        while (true)
        {
            using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(token);
            heartbeat.CancelAfter(TimeSpan.FromSeconds(135));
            var message = await ReadRemoteAsync(tls, heartbeat.Token);
            if (ReadActiveApp(message) is { } package)
                appChanged(package);
            if (ReadVolume(message) is { } volume)
                volumeChanged(volume);
            if (ReadPower(message) is { } power)
                powerChanged(power);
        }
    }

    private static async Task<string?> ConfigureAsync(SslStream tls, uint requested, uint required,
        CancellationToken token, Action<List<Field>>? received = null)
    {
        uint features = 0;
        string? activeApp = null;
        var activated = false;
        while (true)
        {
            var message = await ReadRemoteAsync(tls, token);
            received?.Invoke(message);
            activeApp = ReadActiveApp(message) ?? activeApp;
            var configuration = message.FirstOrDefault(field => field.Number == 1).Data;
            if (configuration is not null)
            {
                var offered = ReadFields(configuration).FirstOrDefault(field => field.Number == 1).Value;
                features = offered & (requested | SystemControl);
                if ((features & required) != required)
                    throw new IOException("The TV does not support the required remote feature.");
                var info = Message(Number(3, 1), Text(4, "1"), Text(5, "Universal TV Controller"), Text(6, "1.0"));
                await SendAsync(tls, Bytes(1, Message(Number(1, features), Bytes(2, info))), token);
            }

            if (!activated && features != 0 && message.Any(field => field.Number == 2))
            {
                await SendAsync(tls, Bytes(2, Number(1, features)), token);
                activated = true;
            }
            if (activated && message.Any(field => field.Number == 40))
                return activeApp;
        }

    }

    private static string? ReadActiveApp(List<Field> message)
    {
        var editor = message.FirstOrDefault(field => field.Number == 20).Data;
        if (editor is null)
            return null;
        var app = ReadFields(editor).FirstOrDefault(field => field.Number == 1).Data;
        if (app is null)
            return null;
        var package = ReadFields(app).FirstOrDefault(field => field.Number == 12).Data;
        return package is null ? null : Encoding.UTF8.GetString(package);
    }

    private static async Task<List<Field>> ReadRemoteAsync(SslStream tls, CancellationToken token)
    {
        var fields = ReadFields(await ReceiveAsync(tls, token));
        if (fields.FirstOrDefault(field => field.Number == 3).Data is { } rejected)
            throw new RemoteCommandRejectedException(ReadFields(rejected).FirstOrDefault(field => field.Number == 1).Value);
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
