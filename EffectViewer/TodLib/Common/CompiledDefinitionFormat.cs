using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace EffectViewer.TodLib.Common
{
    internal sealed class CompiledDefinitionReader
    {
        private readonly byte[] _buffer;
        private int _position;

        public CompiledDefinitionReader(byte[] buffer, int position = 0)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _position = position;
        }

        public int Position => _position;

        public int ReadInt32()
        {
            EnsureAvailable(sizeof(int));
            int value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(_position, sizeof(int)));
            _position += sizeof(int);
            return value;
        }

        public float ReadSingle()
        {
            return BitConverter.Int32BitsToSingle(ReadInt32());
        }

        public byte[] ReadBytes(int count)
        {
            if (count < 0)
            {
                throw new InvalidDataException("Compiled definition contains a negative byte count.");
            }

            EnsureAvailable(count);
            byte[] value = new byte[count];
            Buffer.BlockCopy(_buffer, _position, value, 0, count);
            _position += count;
            return value;
        }

        public string ReadString()
        {
            int length = ReadInt32();
            if (length < 0 || length > 100000)
            {
                throw new InvalidDataException($"Compiled definition contains invalid string length {length}.");
            }

            if (length == 0)
            {
                return string.Empty;
            }

            EnsureAvailable(length);
            string value = Encoding.UTF8.GetString(_buffer, _position, length);
            _position += length;
            return value;
        }

        private void EnsureAvailable(int count)
        {
            if (count < 0 || _position > _buffer.Length - count)
            {
                throw new InvalidDataException("Compiled definition ended unexpectedly.");
            }
        }
    }

    internal sealed class CompiledDefinitionWriter
    {
        private readonly MemoryStream _stream = new();

        public byte[] ToArray()
        {
            return _stream.ToArray();
        }

        public void Write(ReadOnlySpan<byte> bytes)
        {
            _stream.Write(bytes);
        }

        public void WriteInt32(int value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            _stream.Write(buffer);
        }

        public void WriteUInt32(uint value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            _stream.Write(buffer);
        }

        public void WriteSingle(float value)
        {
            WriteInt32(BitConverter.SingleToInt32Bits(value));
        }

        public void WriteString(string value)
        {
            value ??= string.Empty;
            int length = Encoding.UTF8.GetByteCount(value);
            WriteInt32(length);
            if (length == 0)
            {
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            _stream.Write(bytes);
        }
    }

    internal static class CompiledDefinitionFormat
    {
        private const uint Cookie = 0xDEADFED4;
        private const int HeaderSize = sizeof(uint) + sizeof(uint);

        public static bool IsCompiled(Stream stream)
        {
            if (stream is null || !stream.CanSeek || stream.Length - stream.Position < HeaderSize)
            {
                return false;
            }

            long position = stream.Position;
            Span<byte> header = stackalloc byte[sizeof(uint)];
            int read = stream.Read(header);
            stream.Position = position;
            return read == sizeof(uint) && BinaryPrimitives.ReadUInt32LittleEndian(header) == Cookie;
        }

        public static T Load<T>(Stream stream, DefMap<T> defMap, string fileName = "")
        {
            byte[] uncompressed = ReadUncompressed(stream, fileName);
            if (uncompressed.Length < sizeof(uint) + defMap.CompiledSize)
            {
                throw new InvalidDataException($"{Location(fileName)}Compiled file size too small.");
            }

            CompiledDefinitionReader reader = new(uncompressed);
            uint fileHash = unchecked((uint)reader.ReadInt32());
            uint schemaHash = CalcHash(defMap);
            if (fileHash != schemaHash)
            {
                throw new InvalidDataException($"{Location(fileName)}Compiled file schema wrong.");
            }

            T definition = defMap.Constructor();
            byte[] rawDefinition = reader.ReadBytes(defMap.CompiledSize);
            ReadMapFromRaw(reader, defMap, rawDefinition, ref definition);

            if (reader.Position != uncompressed.Length)
            {
                throw new InvalidDataException($"{Location(fileName)}Compiled file wrong size.");
            }

            return definition;
        }

        public static void Save<T>(Stream stream, DefMap<T> defMap, T definition)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            CompiledDefinitionWriter uncompressed = new();
            uncompressed.WriteUInt32(CalcHash(defMap));
            uncompressed.Write(CreateRawStruct(defMap, ref definition));
            WriteMapExtra(uncompressed, defMap, ref definition);

            byte[] uncompressedBytes = uncompressed.ToArray();
            using MemoryStream compressedPayload = new();
            using (ZLibStream zlibStream = new(compressedPayload, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlibStream.Write(uncompressedBytes, 0, uncompressedBytes.Length);
            }

            CompiledDefinitionWriter writer = new();
            writer.WriteUInt32(Cookie);
            writer.WriteUInt32(checked((uint)uncompressedBytes.Length));
            writer.Write(compressedPayload.ToArray());

            byte[] bytes = writer.ToArray();
            stream.Write(bytes, 0, bytes.Length);
        }

        public static byte[] CreateRawStruct<T>(DefMap<T> defMap, ref T definition)
        {
            EnsureCompiledSize(defMap);
            byte[] raw = new byte[defMap.CompiledSize];
            foreach (IDefField<T> field in defMap.Fields)
            {
                field.WriteCompiledRaw(raw, ref definition);
            }

            return raw;
        }

        public static void ReadMapFromRaw<T>(CompiledDefinitionReader reader, DefMap<T> defMap, ReadOnlySpan<byte> rawDefinition, ref T definition)
        {
            EnsureCompiledSize(defMap);
            if (rawDefinition.Length < defMap.CompiledSize)
            {
                throw new InvalidDataException("Compiled definition raw structure is too small.");
            }

            foreach (IDefField<T> field in defMap.Fields)
            {
                field.ReadCompiled(reader, rawDefinition, ref definition);
            }
        }

        public static void WriteMapExtra<T>(CompiledDefinitionWriter writer, DefMap<T> defMap, ref T definition)
        {
            foreach (IDefField<T> field in defMap.Fields)
            {
                field.WriteCompiledExtra(writer, ref definition);
            }
        }

        public static uint CalcHash<T>(DefMap<T> defMap)
        {
            uint hash = Crc32.Empty + 1;
            HashSet<object> progressMaps = new(ReferenceEqualityComparer.Instance);
            AppendMapSchema(ref hash, defMap, progressMaps);
            return hash;
        }

        public static void AppendMapSchema<T>(ref uint schemaHash, DefMap<T> defMap, HashSet<object> progressMaps)
        {
            EnsureCompiledSize(defMap);
            if (!progressMaps.Add(defMap))
            {
                return;
            }

            AppendInt32(ref schemaHash, defMap.CompiledSize);
            foreach (IDefField<T> field in defMap.Fields)
            {
                field.AppendCompiledSchema(ref schemaHash, progressMaps);
            }
        }

        public static void AppendFieldSchema(ref uint schemaHash, DefFieldType fieldType, int offset)
        {
            if (offset < 0)
            {
                throw new InvalidDataException("Compiled field is missing an original structure offset.");
            }

            AppendInt32(ref schemaHash, (int)fieldType);
            AppendInt32(ref schemaHash, offset);
        }

        public static void AppendSymbolSchema(ref uint schemaHash, IReadOnlyList<DefSymbol> symbols)
        {
            if (symbols is null)
            {
                return;
            }

            foreach (DefSymbol symbol in symbols)
            {
                AppendStringBytes(ref schemaHash, symbol.Name ?? string.Empty);
                AppendInt32(ref schemaHash, symbol.Value);
            }
        }

        public static int ReadInt32(ReadOnlySpan<byte> bytes, int offset)
        {
            EnsureOffset(bytes, offset, sizeof(int));
            return BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, sizeof(int)));
        }

        public static float ReadSingle(ReadOnlySpan<byte> bytes, int offset)
        {
            return BitConverter.Int32BitsToSingle(ReadInt32(bytes, offset));
        }

        public static void WriteInt32(Span<byte> bytes, int offset, int value)
        {
            EnsureOffset(bytes, offset, sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(offset, sizeof(int)), value);
        }

        public static void WriteSingle(Span<byte> bytes, int offset, float value)
        {
            WriteInt32(bytes, offset, BitConverter.SingleToInt32Bits(value));
        }

        private static byte[] ReadUncompressed(Stream stream, string fileName)
        {
            byte[] header = new byte[HeaderSize];
            ReadExactly(stream, header);
            uint cookie = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, sizeof(uint)));
            if (cookie != Cookie)
            {
                throw new InvalidDataException($"{Location(fileName)}Compiled file cookie wrong.");
            }

            uint uncompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(sizeof(uint), sizeof(uint)));
            if (uncompressedSize > int.MaxValue)
            {
                throw new InvalidDataException($"{Location(fileName)}Compiled file is too large.");
            }

            using ZLibStream zlibStream = new(stream, CompressionMode.Decompress, leaveOpen: true);
            byte[] uncompressed = new byte[uncompressedSize];
            ReadExactly(zlibStream, uncompressed);
            return uncompressed;
        }

        private static void ReadExactly(Stream stream, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read == 0)
                {
                    throw new InvalidDataException("Compiled definition ended unexpectedly.");
                }

                offset += read;
            }
        }

        private static void AppendInt32(ref uint hash, int value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            hash = Crc32.Update(hash, buffer);
        }

        private static void AppendStringBytes(ref uint hash, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            hash = Crc32.Update(hash, Encoding.UTF8.GetBytes(value));
        }

        private static void EnsureCompiledSize<T>(DefMap<T> defMap)
        {
            if (defMap.CompiledSize <= 0)
            {
                throw new InvalidDataException("Compiled definition map is missing the original structure size.");
            }
        }

        private static void EnsureOffset(ReadOnlySpan<byte> bytes, int offset, int size)
        {
            if (offset < 0 || size < 0 || offset > bytes.Length - size)
            {
                throw new InvalidDataException("Compiled definition raw structure is too small.");
            }
        }

        private static string Location(string fileName)
        {
            return string.IsNullOrWhiteSpace(fileName) ? string.Empty : fileName + ": ";
        }

        private static class Crc32
        {
            public const uint Empty = 0;
            private static readonly uint[] Table = CreateTable();

            public static uint Update(uint crc, ReadOnlySpan<byte> bytes)
            {
                crc = ~crc;
                foreach (byte value in bytes)
                {
                    crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
                }

                return ~crc;
            }

            private static uint[] CreateTable()
            {
                uint[] table = new uint[256];
                for (uint i = 0; i < table.Length; i++)
                {
                    uint crc = i;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
                    }

                    table[i] = crc;
                }

                return table;
            }
        }
    }
}
