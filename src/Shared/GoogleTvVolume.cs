using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static UniversalTvRemote.GoogleTvWire;

namespace UniversalTvRemote;

public static partial class GoogleTvApps
{
    public static Task SetVolumeAsync(IPAddress address, GoogleTvPairing pairing, int volume,
        Action<int>? volumeChanged = null, CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(volume);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(volume, 100);
        return ChangeVolumeAsync(address, pairing, volume, 0, volumeChanged, token);
    }

    public static Task HoldVolumeAsync(IPAddress address, GoogleTvPairing pairing, int direction,
        Action<int>? volumeChanged = null, CancellationToken token = default)
    {
        if (direction is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(direction));
        return ChangeVolumeAsync(address, pairing, null, direction, volumeChanged, token);
    }

    private static async Task ChangeVolumeAsync(IPAddress address, GoogleTvPairing pairing, int? target, int direction,
        Action<int>? volumeChanged, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        using var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(pairing.Certificate), "");
        using var client = new TcpClient(address.AddressFamily);
        await client.ConnectAsync(address, 6466, token);
        using var tls = new SslStream(client.GetStream(), false, (_, peer, _, _) =>
            peer is not null && string.Equals(pairing.Fingerprint,
                Convert.ToHexString(peer.GetCertHash(HashAlgorithmName.SHA256)), StringComparison.Ordinal));
        await AuthenticateAsync(tls, address, certificate, token);

        int? current = null;
        var maximum = 100;
        void ReceiveVolume(List<Field> message)
        {
            if (ReadVolume(message) is not { } level)
                return;
            current = level;
            var data = message.First(field => field.Number == 50).Data!;
            var reportedMaximum = ReadFields(data).FirstOrDefault(field => field.Number == 6).Value;
            if (reportedMaximum > 0)
                maximum = checked((int)reportedMaximum);
            volumeChanged?.Invoke(level);
        }

        await ConfigureAsync(tls, Ping | Keys | Power | Volume, Keys | Volume, token, ReceiveVolume);
        while (current is null)
            ReceiveVolume(await ReadRemoteAsync(tls, token));
        if (target > maximum)
            throw new IOException("The requested volume exceeds the TV's maximum.");

        while (target is null || current != target)
        {
            token.ThrowIfCancellationRequested();
            var previous = current.Value;
            var step = target is { } desired ? Math.Sign(desired - previous) : direction;
            if (step < 0 && previous <= 0 || step > 0 && previous >= maximum)
                return;
            await SendAsync(tls, Bytes(10, Message(Number(1, step > 0 ? 24u : 25u), Number(2, 3))), token);

            // Confirm each step instead of assuming the starting level or that every key arrived.
            using var response = CancellationTokenSource.CreateLinkedTokenSource(token);
            response.CancelAfter(TimeSpan.FromSeconds(3));
            while (current == previous)
                ReceiveVolume(await ReadRemoteAsync(tls, response.Token));
            if (target is { } requested && Math.Sign(requested - current.Value) != step && current != target)
                throw new IOException("The TV cannot confirm the requested volume step.");
            if (target is null)
                await Task.Delay(120, token);
        }
    }
}
