using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace EffectViewer.Tests.TestUtilities;

internal static class PakBuilder
{
    private const byte XorKey = 0xF7;
    private const byte EndFlag = 0x80;
    private const uint Magic = 0xBAC04AC0;

    public static byte[] Create(params (string Path, byte[] Content)[] files)
    {
        using MemoryStream table = new();
        using MemoryStream content = new();

        WriteUInt32(table, Magic);
        WriteUInt32(table, 0);

        foreach ((string path, byte[] bytes) in files)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(path);
            if (nameBytes.Length > byte.MaxValue)
            {
                throw new ArgumentException("PAK fixture paths must fit in one byte.", nameof(files));
            }

            WriteByte(table, 0);
            WriteByte(table, (byte)nameBytes.Length);
            WriteBytes(table, nameBytes);
            WriteInt32(table, bytes.Length);
            WriteInt64(table, 0);
            WriteBytes(content, bytes);
        }

        WriteByte(table, EndFlag);

        using MemoryStream pak = new();
        table.Position = 0;
        table.CopyTo(pak);
        content.Position = 0;
        content.CopyTo(pak);
        return pak.ToArray();
    }

    private static void WriteByte(Stream stream, byte value)
    {
        stream.WriteByte((byte)(value ^ XorKey));
    }

    private static void WriteBytes(Stream stream, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            WriteByte(stream, value);
        }
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        InlineArray4<byte> buffer = new InlineArray4<byte>();
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        WriteBytes(stream, buffer);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        InlineArray4<byte> buffer = new InlineArray4<byte>();
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        WriteBytes(stream, buffer);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        InlineArray8<byte> buffer = new InlineArray8<byte>();
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        WriteBytes(stream, buffer);
    }
}
