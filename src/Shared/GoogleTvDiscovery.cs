using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace UniversalTvRemote;

public sealed record DiscoveredTv(string Name, string Address);

public static class GoogleTvDiscovery
{
    private const string Service = "_androidtvremote2._tcp.local";

    public static async Task<DiscoveredTv[]> FindAsync(CancellationToken token)
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up
                && adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(address => address.Address).Distinct().ToArray();
        var results = await Task.WhenAll(addresses.Select(address => SearchInterfaceAsync(address, token)));
        return results.SelectMany(result => result).DistinctBy(tv => tv.Address)
            .OrderBy(tv => tv.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static async Task<List<DiscoveredTv>> SearchInterfaceAsync(IPAddress local, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        var hosts = new Dictionary<string, IPAddress>(StringComparer.OrdinalIgnoreCase);
        var services = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            udp.Client.Bind(new IPEndPoint(local, 0));
            var target = new IPEndPoint(IPAddress.Parse("224.0.0.251"), 5353);
            await udp.SendAsync(Query(Service, 12), target, timeout.Token);
            while (true)
            {
                var reply = await udp.ReceiveAsync(timeout.Token);
                ReadPacket(reply.Buffer, hosts, services, names);
                foreach (var hostname in services.Values.Distinct().Where(hostname => !hosts.ContainsKey(hostname)).ToArray())
                    await udp.SendAsync(Query(hostname, 1), target, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
        }
        catch (SocketException)
        {
            // A disconnected or isolated interface should not prevent discovery on other interfaces.
        }
        return services.Where(service => hosts.ContainsKey(service.Value))
            .Select(service => new DiscoveredTv(
                names.GetValueOrDefault(service.Key) ?? service.Key.Split("._androidtvremote2.", StringSplitOptions.None)[0],
                hosts[service.Value].ToString())).ToList();
    }

    private static byte[] Query(string name, ushort type)
    {
        using var stream = new MemoryStream();
        stream.Write(new byte[] { 0,0,0,0,0,1,0,0,0,0,0,0 });
        foreach (var label in name.Split('.'))
        {
            var bytes = Encoding.UTF8.GetBytes(label);
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes);
        }
        stream.WriteByte(0);
        stream.Write(new[] { (byte)(type >> 8), (byte)type, (byte)0x80, (byte)1 });
        return stream.ToArray();
    }

    private static void ReadPacket(byte[] packet, Dictionary<string, IPAddress> hosts,
        Dictionary<string, string> services, Dictionary<string, string> names)
    {
        try
        {
            if (packet.Length < 12)
                return;
            var questions = Word(packet, 4);
            var records = Word(packet, 6) + Word(packet, 8) + Word(packet, 10);
            var offset = 12;
            for (var index = 0; index < questions; index++)
            {
                ReadName(packet, ref offset);
                offset += 4;
            }
            for (var index = 0; index < records; index++)
            {
                var name = ReadName(packet, ref offset);
                var type = Word(packet, offset);
                var length = Word(packet, offset + 8);
                offset += 10;
                var end = offset + length;
                if (end > packet.Length)
                    return;
                if (type == 1 && length == 4)
                    hosts[name] = new IPAddress(packet.AsSpan(offset, 4));
                else if (type == 33 && length >= 7 && name.EndsWith(Service, StringComparison.OrdinalIgnoreCase))
                {
                    var port = Word(packet, offset + 4);
                    var position = offset + 6;
                    if (port == 6466)
                        services[name] = ReadName(packet, ref position);
                }
                else if (type == 16)
                {
                    var position = offset;
                    while (position < end)
                    {
                        var size = packet[position++];
                        if (position + size > end)
                            break;
                        var entry = Encoding.UTF8.GetString(packet, position, size);
                        if (entry.StartsWith("fn=", StringComparison.OrdinalIgnoreCase))
                            names[name] = entry[3..];
                        position += size;
                    }
                }
                offset = end;
            }
        }
        catch (Exception error) when (error is IOException or ArgumentOutOfRangeException)
        {
            // Ignore malformed datagrams from the local network.
        }
    }

    private static string ReadName(byte[] packet, ref int offset)
    {
        var labels = new List<string>();
        var cursor = offset;
        var jumped = false;
        for (var hops = 0; hops < 128; hops++)
        {
            if (cursor >= packet.Length)
                break;
            var size = packet[cursor++];
            if (size == 0)
            {
                if (!jumped)
                    offset = cursor;
                return string.Join('.', labels);
            }
            if ((size & 0xC0) == 0xC0)
            {
                if (cursor >= packet.Length)
                    break;
                if (!jumped)
                    offset = cursor + 1;
                cursor = ((size & 0x3F) << 8) | packet[cursor];
                jumped = true;
                continue;
            }
            if (size > 63 || cursor + size > packet.Length)
                break;
            labels.Add(Encoding.UTF8.GetString(packet, cursor, size));
            cursor += size;
        }
        throw new IOException("Invalid DNS name.");
    }

    private static ushort Word(byte[] packet, int offset)
    {
        if (offset < 0 || offset + 2 > packet.Length)
            throw new IOException("Truncated DNS record.");
        return BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(offset, 2));
    }
}
