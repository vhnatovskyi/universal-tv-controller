using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BraviaRemote;

internal sealed class BraviaClient(IPAddress address, int port = 20060)
{
    private const int FrameLength = 24;

    public Task<int> GetVolumeAsync(CancellationToken cancellationToken = default) =>
        ExchangeAsync("VOLU", cancellationToken: cancellationToken);
    public Task PressAsync(int key) => ExchangeAsync("IRCC", key);
    public Task SetPowerAsync(bool on) => ExchangeAsync("POWR", on ? 1 : 0);

    private async Task<int> ExchangeAsync(
        string function, int? argument = null, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        using var socket = new TcpClient(address.AddressFamily);
        await socket.ConnectAsync(address, port, timeout.Token).ConfigureAwait(false);

        var stream = socket.GetStream();
        var type = argument.HasValue ? 'C' : 'E';
        var payload = argument?.ToString("D16", CultureInfo.InvariantCulture) ?? "################";
        var request = Encoding.ASCII.GetBytes($"*S{type}{function}{payload}\n");
        await stream.WriteAsync(request, timeout.Token).ConfigureAwait(false);

        var buffer = new byte[FrameLength];
        var answerPrefix = "*SA" + function;
        while (true)
        {
            await stream.ReadExactlyAsync(buffer, timeout.Token).ConfigureAwait(false);
            var frame = Encoding.ASCII.GetString(buffer);
            if (frame[^1] != '\n')
                throw new IOException("Телевізор надіслав відповідь невідомого формату.");

            // BRAVIA can send notifications before the answer to this command.
            if (frame.StartsWith("*SN", StringComparison.Ordinal))
                continue;

            if (!frame.StartsWith(answerPrefix, StringComparison.Ordinal))
                throw new IOException("Телевізор надіслав неочікувану відповідь.");

            var value = frame.AsSpan(answerPrefix.Length, 16);
            if (value.SequenceEqual("FFFFFFFFFFFFFFFF") || value.SequenceEqual("NNNNNNNNNNNNNNNN"))
                throw new IOException("Телевізор відхилив команду або не підтримує її.");

            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
                || (argument.HasValue && result != 0))
                throw new IOException("Телевізор не підтвердив виконання команди.");

            return result;
        }
    }
}
