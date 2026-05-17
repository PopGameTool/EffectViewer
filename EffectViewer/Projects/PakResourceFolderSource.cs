using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EffectViewer.Projects
{
    public sealed class PakResourceFolderSource : IResourceFolderSource
    {
        private const byte XorKey = 0xF7;
        private const byte EndFlag = 0x80;
        private const uint Magic = 0xBAC04AC0;
        private const string ImportOperation = "Importing Resource Folder";

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly byte[] _data;
        private readonly IReadOnlyList<ResourceFolderFile> _files;
        private readonly Dictionary<string, PakEntry> _entries;
        private readonly HashSet<string> _directories;

        public PakResourceFolderSource(string sourceName, byte[] data)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            _data = data;
            Name = CreateSourceName(sourceName);

            List<ResourceFolderFile> files = [];
            _entries = new Dictionary<string, PakEntry>(StringComparer.OrdinalIgnoreCase);
            _directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };
            Parse(files);
            _files = files;
        }

        public string Name { get; }

        public static async Task<PakResourceFolderSource> FromStreamAsync(string sourceName, Stream sourceStream)
        {
            if (sourceStream is null)
            {
                throw new ArgumentNullException(nameof(sourceStream));
            }

            using MemoryStream buffer = new();
            await sourceStream.CopyToAsync(buffer);
            return new PakResourceFolderSource(sourceName, buffer.ToArray());
        }

        public Task<IReadOnlyList<ResourceFolderFile>> EnumerateFilesAsync(IProgress<ProjectTransferProgress> progress = null)
        {
            progress?.Report(new ProjectTransferProgress
            {
                Operation = ImportOperation,
                Message = $"Scanned {_files.Count} file(s) from {Name}",
                CompletedItems = _files.Count
            });
            return Task.FromResult(_files);
        }

        public Task<bool> DirectoryExistsAsync(string relativePath)
        {
            string normalized = ResourceFolderPath.Normalize(relativePath);
            return Task.FromResult(string.IsNullOrWhiteSpace(normalized) || _directories.Contains(normalized));
        }

        public Task<Stream> OpenReadAsync(string relativePath)
        {
            string normalized = ResourceFolderPath.Normalize(relativePath);
            if (!_entries.TryGetValue(normalized, out PakEntry entry))
            {
                throw new FileNotFoundException("The pak archive does not contain the requested file.", relativePath);
            }

            return Task.FromResult<Stream>(new PakEntryStream(_data, entry.Offset, entry.Size));
        }

        private static string CreateSourceName(string sourceName)
        {
            string fileName = Path.GetFileNameWithoutExtension(sourceName ?? string.Empty);
            return string.IsNullOrWhiteSpace(fileName) ? "Imported Pak" : fileName;
        }

        private void Parse(List<ResourceFolderFile> files)
        {
            int position = 0;
            uint magic = ReadUInt32(ref position);
            if (magic != Magic)
            {
                throw new InvalidDataException("The selected file is not a supported pak archive.");
            }

            uint version = ReadUInt32(ref position);
            if (version > 0)
            {
                throw new InvalidDataException("The selected pak archive version is not supported.");
            }

            List<PakEntry> entries = [];
            int dataOffset = 0;
            while (position < _data.Length)
            {
                byte flags = ReadByte(ref position);
                if ((flags & EndFlag) != 0)
                {
                    break;
                }

                byte nameLength = ReadByte(ref position);
                string rawName = ReadName(ref position, nameLength);
                int size = ReadInt32(ref position);
                _ = ReadInt64(ref position);
                if (size < 0)
                {
                    throw new InvalidDataException("The pak archive contains a file with an invalid size.");
                }

                string relativePath = ResourceFolderPath.Normalize(rawName);
                if (!string.IsNullOrWhiteSpace(relativePath) &&
                    ResourceFolderPath.IsSafeRelativePath(relativePath) &&
                    !_entries.ContainsKey(relativePath))
                {
                    PakEntry entry = new(relativePath, dataOffset, size);
                    entries.Add(entry);
                    _entries.Add(relativePath, entry);
                    files.Add(new ResourceFolderFile(relativePath));
                    AddDirectoryTree(relativePath);
                }

                checked
                {
                    dataOffset += size;
                }
            }

            int contentStart = position;
            foreach (PakEntry entry in entries)
            {
                long offset = (long)contentStart + entry.Offset;
                if (offset > _data.Length || entry.Size > _data.Length - offset)
                {
                    throw new InvalidDataException("The pak archive file table points outside the archive data.");
                }

                entry.Offset = (int)offset;
            }

            files.Sort((left, right) => string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase));
        }

        private void AddDirectoryTree(string relativePath)
        {
            string directory = ResourceFolderPath.GetDirectoryName(relativePath);
            while (!string.IsNullOrWhiteSpace(directory))
            {
                _directories.Add(directory);
                directory = ResourceFolderPath.GetDirectoryName(directory);
            }
        }

        private byte ReadByte(ref int position)
        {
            EnsureAvailable(position, 1);
            return (byte)(_data[position++] ^ XorKey);
        }

        private int ReadInt32(ref int position)
        {
            InlineArray4<byte> buffer = new InlineArray4<byte>();
            ReadBytes(ref position, buffer);
            return BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }

        private uint ReadUInt32(ref int position)
        {
            InlineArray4<byte> buffer = new InlineArray4<byte>();
            ReadBytes(ref position, buffer);
            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        private long ReadInt64(ref int position)
        {
            InlineArray8<byte> buffer = new InlineArray8<byte>();
            ReadBytes(ref position, buffer);
            return BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }

        private string ReadName(ref int position, int length)
        {
            byte[] bytes = new byte[length];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = ReadByte(ref position);
            }

            try
            {
                return StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Latin1.GetString(bytes);
            }
        }

        private void ReadBytes(ref int position, Span<byte> destination)
        {
            EnsureAvailable(position, destination.Length);
            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = (byte)(_data[position++] ^ XorKey);
            }
        }

        private void EnsureAvailable(int position, int count)
        {
            if (position < 0 || count < 0 || position > _data.Length - count)
            {
                throw new InvalidDataException("The pak archive is truncated.");
            }
        }

        private sealed class PakEntry
        {
            public PakEntry(string relativePath, int offset, int size)
            {
                RelativePath = relativePath;
                Offset = offset;
                Size = size;
            }

            public string RelativePath { get; }
            public int Offset { get; set; }
            public int Size { get; }
        }

        private sealed class PakEntryStream : Stream
        {
            private readonly byte[] _data;
            private readonly int _offset;
            private readonly int _size;
            private int _position;

            public PakEntryStream(byte[] data, int offset, int size)
            {
                _data = data;
                _offset = offset;
                _size = size;
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => _size;

            public override long Position
            {
                get => _position;
                set => _position = checked((int)Math.Clamp(value, 0, _size));
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }

            public override int Read(Span<byte> buffer)
            {
                int readable = Math.Min(buffer.Length, _size - _position);
                if (readable <= 0)
                {
                    return 0;
                }

                for (int i = 0; i < readable; i++)
                {
                    buffer[i] = (byte)(_data[_offset + _position + i] ^ XorKey);
                }

                _position += readable;
                return readable;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return ValueTask.FromCanceled<int>(cancellationToken);
                }

                return ValueTask.FromResult(Read(buffer.Span));
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long target = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => _position + offset,
                    SeekOrigin.End => _size + offset,
                    _ => throw new ArgumentOutOfRangeException(nameof(origin))
                };

                Position = target;
                return _position;
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
