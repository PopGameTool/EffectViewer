using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EffectViewer.Rendering.Export
{
    public static class GifImageWriter
    {
        public static void Write(Stream stream, int width, int height, IReadOnlyList<byte[]> rgbaFrames, int fps)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "GIF dimensions must be positive.");
            }

            if (rgbaFrames is null || rgbaFrames.Count == 0)
            {
                throw new ArgumentException("At least one frame is required.", nameof(rgbaFrames));
            }

            fps = Math.Clamp(fps, 1, 240);
            int delay = Math.Max(1, (int)Math.Round(100d / fps));

            WriteAscii(stream, "GIF89a");
            WriteUInt16(stream, width);
            WriteUInt16(stream, height);
            stream.WriteByte(0xF7);
            stream.WriteByte(0);
            stream.WriteByte(0);
            WriteGlobalPalette(stream);
            WriteLoopExtension(stream);

            foreach (byte[] frame in rgbaFrames)
            {
                if (frame is null || frame.Length < width * height * 4)
                {
                    throw new ArgumentException("Frame pixel data is incomplete.", nameof(rgbaFrames));
                }

                byte[] indexed = ToIndexedPixels(frame, width, height, out bool hasTransparentPixels);
                WriteGraphicControlExtension(stream, delay, hasTransparentPixels);
                WriteImageDescriptor(stream, width, height);
                stream.WriteByte(8);
                WriteSubBlocks(stream, LzwEncode(indexed));
            }

            stream.WriteByte(0x3B);
        }

        private static void WriteGlobalPalette(Stream stream)
        {
            for (int i = 0; i < 256; i++)
            {
                int red = ((i >> 5) & 0x07) * 255 / 7;
                int green = ((i >> 2) & 0x07) * 255 / 7;
                int blue = (i & 0x03) * 255 / 3;
                stream.WriteByte((byte)red);
                stream.WriteByte((byte)green);
                stream.WriteByte((byte)blue);
            }
        }

        private static byte[] ToIndexedPixels(byte[] rgba, int width, int height, out bool hasTransparentPixels)
        {
            byte[] indexed = new byte[width * height];
            hasTransparentPixels = false;
            for (int i = 0, p = 0; i < indexed.Length; i++, p += 4)
            {
                byte alpha = rgba[p + 3];
                if (alpha < 8)
                {
                    indexed[i] = 0;
                    hasTransparentPixels = true;
                    continue;
                }

                int red = rgba[p + 0];
                int green = rgba[p + 1];
                int blue = rgba[p + 2];
                indexed[i] = (byte)Math.Max(1, ((red >> 5) << 5) | ((green >> 5) << 2) | (blue >> 6));
            }

            return indexed;
        }

        private static void WriteLoopExtension(Stream stream)
        {
            stream.WriteByte(0x21);
            stream.WriteByte(0xFF);
            stream.WriteByte(11);
            WriteAscii(stream, "NETSCAPE2.0");
            stream.WriteByte(3);
            stream.WriteByte(1);
            WriteUInt16(stream, 0);
            stream.WriteByte(0);
        }

        private static void WriteGraphicControlExtension(Stream stream, int delay, bool hasTransparentPixels)
        {
            stream.WriteByte(0x21);
            stream.WriteByte(0xF9);
            stream.WriteByte(4);
            stream.WriteByte((byte)(0x04 | (hasTransparentPixels ? 0x01 : 0x00)));
            WriteUInt16(stream, delay);
            stream.WriteByte(0);
            stream.WriteByte(0);
        }

        private static void WriteImageDescriptor(Stream stream, int width, int height)
        {
            stream.WriteByte(0x2C);
            WriteUInt16(stream, 0);
            WriteUInt16(stream, 0);
            WriteUInt16(stream, width);
            WriteUInt16(stream, height);
            stream.WriteByte(0);
        }

        private static byte[] LzwEncode(byte[] indices)
        {
            const int clearCode = 256;
            const int endCode = 257;
            const int firstCode = 258;

            Dictionary<int, int> dictionary = new();

            using MemoryStream output = new();
            BitWriter writer = new(output);
            int codeSize = 9;
            int nextCode = firstCode;

            writer.Write(clearCode, codeSize);
            int prefix = indices.Length == 0 ? 0 : indices[0];
            for (int i = 1; i < indices.Length; i++)
            {
                int value = indices[i];
                int key = (prefix << 8) | value;
                if (dictionary.TryGetValue(key, out int code))
                {
                    prefix = code;
                    continue;
                }

                writer.Write(prefix, codeSize);
                if (nextCode < 4096)
                {
                    dictionary[key] = nextCode++;
                    if (nextCode > (1 << codeSize) && codeSize < 12)
                    {
                        codeSize++;
                    }
                }
                else
                {
                    writer.Write(clearCode, codeSize);
                    dictionary.Clear();
                    codeSize = 9;
                    nextCode = firstCode;
                }

                prefix = value;
            }

            writer.Write(prefix, codeSize);
            writer.Write(endCode, codeSize);
            writer.Flush();
            return output.ToArray();
        }

        private static void WriteSubBlocks(Stream stream, byte[] data)
        {
            int offset = 0;
            while (offset < data.Length)
            {
                int count = Math.Min(255, data.Length - offset);
                stream.WriteByte((byte)count);
                stream.Write(data, offset, count);
                offset += count;
            }

            stream.WriteByte(0);
        }

        private static void WriteUInt16(Stream stream, int value)
        {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
        }

        private static void WriteAscii(Stream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private sealed class BitWriter
        {
            private readonly Stream _stream;
            private int _buffer;
            private int _bitCount;

            public BitWriter(Stream stream)
            {
                _stream = stream;
            }

            public void Write(int code, int bitCount)
            {
                _buffer |= code << _bitCount;
                _bitCount += bitCount;
                while (_bitCount >= 8)
                {
                    _stream.WriteByte((byte)(_buffer & 0xFF));
                    _buffer >>= 8;
                    _bitCount -= 8;
                }
            }

            public void Flush()
            {
                if (_bitCount > 0)
                {
                    _stream.WriteByte((byte)(_buffer & 0xFF));
                    _buffer = 0;
                    _bitCount = 0;
                }
            }
        }
    }
}
