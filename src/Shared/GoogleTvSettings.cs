using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static UniversalTvRemote.GoogleTvWire;

namespace UniversalTvRemote;

public static partial class GoogleTvApps
{
    public static async Task OpenSettingsAsync(IPAddress address, GoogleTvPairing pairing,
        CancellationToken cancellation = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var token = timeout.Token;
        using var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(pairing.Certificate), "");
        using var client = new TcpClient(address.AddressFamily);
        await client.ConnectAsync(address, 6466, token);
        using var tls = new SslStream(client.GetStream(), false, (_, peer, _, _) =>
            peer is not null && string.Equals(pairing.Fingerprint,
                Convert.ToHexString(peer.GetCertHash(HashAlgorithmName.SHA256)), StringComparison.Ordinal));
        await AuthenticateAsync(tls, address, certificate, token);
        await ConfigureAsync(tls, Ping | Keys | Power | Volume | CurrentApp | AppLinks, Keys, token);

        // BRAVIA 3 ignores SETTINGS; holding HOME opens its settings side panel.
        await SendAsync(tls, Bytes(10, Message(Number(1, 3), Number(2, 1))), token);
        try
        {
            await Task.Delay(1200, token);
        }
        finally
        {
            using var release = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await SendAsync(tls, Bytes(10, Message(Number(1, 3), Number(2, 2))), release.Token);
        }
    }
}
