using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace BraviaRemote;

public static partial class GoogleTvApps
{
    internal static byte[] CreateWakePacket(string macAddress)
    {
        var normalized = macAddress.Replace(":", "").Replace("-", "");
        if (normalized.Length != 12 || !normalized.All(Uri.IsHexDigit))
            throw new ArgumentException("A six-byte MAC address is required.", nameof(macAddress));
        var mac = Convert.FromHexString(normalized);
        if (mac.All(value => value == 0) || (mac[0] & 1) != 0)
            throw new ArgumentException("A unicast MAC address is required.", nameof(macAddress));
        var packet = new byte[102];
        Array.Fill(packet, (byte)255, 0, 6);
        for (var index = 0; index < 16; index++)
            mac.CopyTo(packet, 6 + index * 6);
        return packet;
    }

    public static async Task WakeAsync(IPAddress address, GoogleTvPairing pairing, string macAddress,
        CancellationToken cancellation = default)
    {
        var packet = CreateWakePacket(macAddress);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        var token = timeout.Token;
        var broadcasts = new HashSet<IPAddress> { IPAddress.Broadcast };
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(item => item.OperationalStatus == OperationalStatus.Up))
        foreach (var local in adapter.GetIPProperties().UnicastAddresses.Where(item => item.Address.AddressFamily == AddressFamily.InterNetwork))
        {
            var bytes = local.Address.GetAddressBytes();
            var mask = local.IPv4Mask.GetAddressBytes();
            var target = address.GetAddressBytes();
            if (target.Length == 4 && Enumerable.Range(0, 4).All(index => (bytes[index] & mask[index]) == (target[index] & mask[index])))
                broadcasts.Add(new IPAddress(Enumerable.Range(0, 4).Select(index => (byte)(bytes[index] | ~mask[index])).ToArray()));
        }

        using var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(pairing.Certificate), "");
        using var udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
        try
        {
            while (true)
            {
                foreach (var broadcast in broadcasts)
                    await udp.SendAsync(packet, new IPEndPoint(broadcast, 9), token);
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(token);
                attempt.CancelAfter(TimeSpan.FromSeconds(3));
                try
                {
                    using var client = new TcpClient(address.AddressFamily);
                    await client.ConnectAsync(address, 6466, attempt.Token);
                    using var tls = new SslStream(client.GetStream(), false, (_, peer, _, _) =>
                        peer is not null && string.Equals(pairing.Fingerprint,
                            Convert.ToHexString(peer.GetCertHash(HashAlgorithmName.SHA256)), StringComparison.Ordinal));
                    await AuthenticateAsync(tls, address, certificate, attempt.Token);
                    var on = false;
                    await ConfigureAsync(tls, Ping | Power | CurrentApp | Volume, Power, attempt.Token, message =>
                        on = ReadPower(message) ?? on);
                    if (on)
                        return;
                    // A wake packet is idempotent; never retry POWER, which could switch an awakening TV off again.
                }
                catch (Exception error) when (error is SocketException or IOException or OperationCanceledException)
                {
                    token.ThrowIfCancellationRequested();
                }
                await Task.Delay(500, token);
            }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new TvPowerUnconfirmedException(expectedOn: true);
        }
    }
}
