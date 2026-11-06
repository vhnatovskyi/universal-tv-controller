using System.Text;

namespace BraviaRemote;

internal static class GoogleTvWire
{
    internal readonly record struct Field(int Number, uint Value, byte[]? Data);

    public static byte[] Message(params byte[][] fields) => fields.SelectMany(field => field).ToArray();
    public static byte[] Number(int field, uint value) => Message(Varint((uint)field << 3), Varint(value));
    public static byte[] Text(int field, string text) => Bytes(field, Encoding.UTF8.GetBytes(text));
    public static byte[] Bytes(int field, byte[] data) =>
        Message(Varint(((uint)field << 3) | 2), Varint((uint)data.Length), data);

    public static async Task SendAsync(Stream stream, byte[] message, CancellationToken token) =>
        await stream.WriteAsync(Message(Varint((uint)message.Length), message), token);

    public static async Task<byte[]> ReceiveAsync(Stream stream, CancellationToken token)
    {
        var next = new byte[1];
        uint size = 0;
        for (var shift = 0; shift <= 14; shift += 7)
        {
            await stream.ReadExactlyAsync(next, token);
            size |= (uint)(next[0] & 127) << shift;
            if ((next[0] & 128) != 0)
                continue;
            if (size is 0 or > 65536)
                break;

            var data = new byte[size];
            await stream.ReadExactlyAsync(data, token);
            return data;
        }
        throw new IOException("Invalid Google TV message length.");
    }

    public static List<Field> ReadFields(byte[] message)
    {
        var fields = new List<Field>();
        var index = 0;
        while (index < message.Length)
        {
            var tag = ReadVarint(message, ref index);
            var number = (int)(tag >> 3);
            switch (tag & 7)
            {
                case 0:
                    fields.Add(new(number, ReadVarint(message, ref index), null));
                    break;
                case 2:
                    var length = ReadVarint(message, ref index);
                    if (length > message.Length - index)
                        throw new IOException("Invalid Google TV field length.");
                    fields.Add(new(number, 0, message.AsSpan(index, (int)length).ToArray()));
                    index += (int)length;
                    break;
                case 1:
                case 5:
                    index += (tag & 7) == 1 ? 8 : 4;
                    if (index > message.Length)
                        throw new IOException("Invalid Google TV fixed-width field.");
                    break;
                default:
                    throw new IOException("Unsupported Google TV wire type.");
            }
        }
        return fields;
    }

    private static uint ReadVarint(byte[] data, ref int index)
    {
        uint result = 0;
        for (var shift = 0; shift <= 28 && index < data.Length; shift += 7)
        {
            var next = data[index++];
            result |= (uint)(next & 127) << shift;
            if ((next & 128) == 0)
                return result;
        }
        throw new IOException("Invalid Google TV varint.");
    }

    private static byte[] Varint(uint value)
    {
        var result = new List<byte>(5);
        while (value > 127)
        {
            result.Add((byte)((value & 127) | 128));
            value >>= 7;
        }
        result.Add((byte)value);
        return result.ToArray();
    }
}
