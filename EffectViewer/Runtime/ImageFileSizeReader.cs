using System;
using System.Buffers.Binary;
using System.IO;

namespace EffectViewer.Runtime
{
    internal static class ImageFileSizeReader
    {
        public static bool TryReadSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                using FileStream stream = File.OpenRead(path);
                return TryReadPngSize(stream, out width, out height) ||
                       TryReadJpegSize(stream, out width, out height) ||
                       TryReadGifSize(stream, out width, out height);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool TryReadPngSize(Stream stream, out int width, out int height)
        {
            width = 0;
            height = 0;
            stream.Position = 0;
            Span<byte> header = stackalloc byte[24];
            if (stream.Read(header) != header.Length ||
                header[0] != 0x89 ||
                header[1] != (byte)'P' ||
                header[2] != (byte)'N' ||
                header[3] != (byte)'G')
            {
                return false;
            }

            width = BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
            height = BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
            return width > 0 && height > 0;
        }

        private static bool TryReadJpegSize(Stream stream, out int width, out int height)
        {
            width = 0;
            height = 0;
            stream.Position = 0;
            if (ReadByte(stream) != 0xFF || ReadByte(stream) != 0xD8)
            {
                return false;
            }

            while (stream.Position < stream.Length)
            {
                int prefix = ReadByte(stream);
                if (prefix != 0xFF)
                {
                    continue;
                }

                int marker;
                do
                {
                    marker = ReadByte(stream);
                }
                while (marker == 0xFF);

                if (marker == 0xD9 || marker == 0xDA || marker < 0)
                {
                    return false;
                }

                int length = ReadUInt16BigEndian(stream);
                if (length < 2)
                {
                    return false;
                }

                if (IsStartOfFrame(marker))
                {
                    if (ReadByte(stream) < 0)
                    {
                        return false;
                    }

                    height = ReadUInt16BigEndian(stream);
                    width = ReadUInt16BigEndian(stream);
                    return width > 0 && height > 0;
                }

                stream.Position = Math.Min(stream.Length, stream.Position + length - 2);
            }

            return false;
        }

        private static bool TryReadGifSize(Stream stream, out int width, out int height)
        {
            width = 0;
            height = 0;
            stream.Position = 0;
            Span<byte> header = stackalloc byte[10];
            if (stream.Read(header) != header.Length ||
                header[0] != (byte)'G' ||
                header[1] != (byte)'I' ||
                header[2] != (byte)'F')
            {
                return false;
            }

            width = BinaryPrimitives.ReadUInt16LittleEndian(header[6..8]);
            height = BinaryPrimitives.ReadUInt16LittleEndian(header[8..10]);
            return width > 0 && height > 0;
        }

        private static bool IsStartOfFrame(int marker)
        {
            return marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF;
        }

        private static int ReadByte(Stream stream)
        {
            return stream.ReadByte();
        }

        private static int ReadUInt16BigEndian(Stream stream)
        {
            int high = ReadByte(stream);
            int low = ReadByte(stream);
            return high < 0 || low < 0 ? -1 : (high << 8) | low;
        }
    }
}
