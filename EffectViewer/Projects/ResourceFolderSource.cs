using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace EffectViewer.Projects
{
    public sealed class ResourceFolderFile
    {
        public ResourceFolderFile(string relativePath)
        {
            RelativePath = ResourceFolderPath.Normalize(relativePath);
        }

        public string RelativePath { get; }
        public string Name => ResourceFolderPath.GetFileName(RelativePath);
    }

    public interface IResourceFolderSource
    {
        string Name { get; }
        Task<IReadOnlyList<ResourceFolderFile>> EnumerateFilesAsync(IProgress<ProjectTransferProgress> progress = null);
        Task<bool> DirectoryExistsAsync(string relativePath);
        Task<Stream> OpenReadAsync(string relativePath);
    }

    public sealed class LocalResourceFolderSource : IResourceFolderSource
    {
        private readonly string _rootPath;

        public LocalResourceFolderSource(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                throw new DirectoryNotFoundException(rootPath);
            }

            _rootPath = Path.GetFullPath(rootPath);
            Name = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        public string Name { get; }

        public Task<IReadOnlyList<ResourceFolderFile>> EnumerateFilesAsync(IProgress<ProjectTransferProgress> progress = null)
        {
            IReadOnlyList<ResourceFolderFile> files = Directory
                .EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories)
                .Select(file => new ResourceFolderFile(Path.GetRelativePath(_rootPath, file)))
                .ToList();
            progress?.Report(new ProjectTransferProgress
            {
                Operation = "Importing Resource Folder",
                Message = $"Scanned {files.Count} file(s)",
                CompletedItems = files.Count
            });
            return Task.FromResult(files);
        }

        public Task<bool> DirectoryExistsAsync(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(Directory.Exists(ResolvePath(relativePath)));
        }

        public Task<Stream> OpenReadAsync(string relativePath)
        {
            return Task.FromResult<Stream>(File.OpenRead(ResolvePath(relativePath)));
        }

        private string ResolvePath(string relativePath)
        {
            string normalized = ResourceFolderPath.Normalize(relativePath);
            if (!ResourceFolderPath.IsSafeRelativePath(normalized))
            {
                throw new InvalidOperationException("The source path points outside the selected folder.");
            }

            string localPath = normalized.Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(_rootPath, localPath));
            if (!fullPath.StartsWith(_rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fullPath, _rootPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The source path points outside the selected folder.");
            }

            return fullPath;
        }
    }

    public sealed class StorageResourceFolderSource : IResourceFolderSource, IDisposable
    {
        private readonly IStorageFolder _root;
        private readonly List<IStorageItem> _storageItems = [];
        private IReadOnlyList<ResourceFolderFile> _files;
        private Dictionary<string, IStorageFile> _fileItems;
        private HashSet<string> _directories;
        private int _scannedFileCount;
        private int _skippedDirectoryCount;

        public StorageResourceFolderSource(IStorageFolder root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            Name = string.IsNullOrWhiteSpace(root.Name) ? "project" : root.Name;
        }

        public string Name { get; }

        public async Task<IReadOnlyList<ResourceFolderFile>> EnumerateFilesAsync(IProgress<ProjectTransferProgress> progress = null)
        {
            await EnsureLoadedAsync(progress);
            return _files;
        }

        public async Task<bool> DirectoryExistsAsync(string relativePath)
        {
            await EnsureLoadedAsync();
            string normalized = ResourceFolderPath.Normalize(relativePath);
            return string.IsNullOrWhiteSpace(normalized) ||
                   _directories.Contains(normalized) ||
                   _fileItems.Keys.Any(path => IsInDirectoryTree(path, normalized));
        }

        public async Task<Stream> OpenReadAsync(string relativePath)
        {
            await EnsureLoadedAsync();
            string normalized = ResourceFolderPath.Normalize(relativePath);
            if (!_fileItems.TryGetValue(normalized, out IStorageFile file))
            {
                throw new FileNotFoundException("The selected folder does not contain the requested file.", relativePath);
            }

            await using Stream source = await file.OpenReadAsync();
            MemoryStream buffer = new();
            await source.CopyToAsync(buffer);
            buffer.Position = 0;
            return buffer;
        }

        public void Dispose()
        {
            _root.Dispose();
            foreach (IStorageItem item in _storageItems)
            {
                item.Dispose();
            }

            _storageItems.Clear();
        }

        private async Task EnsureLoadedAsync(IProgress<ProjectTransferProgress> progress = null)
        {
            if (_files is not null)
            {
                return;
            }

            List<ResourceFolderFile> files = [];
            _fileItems = new Dictionary<string, IStorageFile>(StringComparer.OrdinalIgnoreCase);
            _directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };
            _scannedFileCount = 0;
            _skippedDirectoryCount = 0;
            try
            {
                await ReadFolderAsync(_root, string.Empty, files, progress, isRoot: true);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException($"Could not read the selected folder: {ex.Message}", ex);
            }

            progress?.Report(new ProjectTransferProgress
            {
                Operation = "Importing Resource Folder",
                Message = _skippedDirectoryCount == 0
                    ? $"Scanned {files.Count} file(s)"
                    : $"Scanned {files.Count} file(s), skipped {_skippedDirectoryCount} folder(s)",
                CompletedItems = files.Count
            });
            _files = files;
        }

        private async Task<bool> ReadFolderAsync(
            IStorageFolder folder,
            string relativeDirectory,
            List<ResourceFolderFile> files,
            IProgress<ProjectTransferProgress> progress,
            bool isRoot = false)
        {
            progress?.Report(new ProjectTransferProgress
            {
                Operation = "Importing Resource Folder",
                Message = string.IsNullOrWhiteSpace(relativeDirectory)
                    ? $"Scanning {Name}"
                    : $"Scanning {relativeDirectory}",
                CompletedItems = _scannedFileCount
            });
            await Task.Yield();

            IAsyncEnumerable<IStorageItem> items;
            try
            {
                items = folder.GetItemsAsync();
            }
            catch (Exception ex)
            {
                if (isRoot)
                {
                    string name = string.IsNullOrWhiteSpace(relativeDirectory) ? Name : relativeDirectory;
                    throw new InvalidOperationException($"Could not list {name}: {ex.Message}", ex);
                }

                ReportSkippedDirectory(relativeDirectory, ex, progress);
                return false;
            }

            try
            {
                await foreach (IStorageItem item in items)
                {
                    _storageItems.Add(item);
                    switch (item)
                    {
                        case IStorageFolder childFolder:
                        {
                            string childDirectory = ResourceFolderPath.Combine(relativeDirectory, childFolder.Name);
                            if (await ReadFolderAsync(childFolder, childDirectory, files, progress))
                            {
                                _directories.Add(childDirectory);
                            }

                            break;
                        }
                        case IStorageFile file:
                        {
                            string relativePath = ResourceFolderPath.Combine(relativeDirectory, file.Name);
                            ResourceFolderFile sourceFile = new(relativePath);
                            files.Add(sourceFile);
                            _fileItems[sourceFile.RelativePath] = file;
                            _scannedFileCount++;
                            if (_scannedFileCount % 25 == 0)
                            {
                                progress?.Report(new ProjectTransferProgress
                                {
                                    Operation = "Importing Resource Folder",
                                    Message = $"Scanning folder ({_scannedFileCount} file(s))",
                                    CompletedItems = _scannedFileCount
                                });
                                await Task.Yield();
                            }

                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (isRoot)
                {
                    string name = string.IsNullOrWhiteSpace(relativeDirectory) ? Name : relativeDirectory;
                    throw new InvalidOperationException($"Could not list {name}: {ex.Message}", ex);
                }

                ReportSkippedDirectory(relativeDirectory, ex, progress);
                return false;
            }

            return true;
        }

        private void ReportSkippedDirectory(
            string relativeDirectory,
            Exception exception,
            IProgress<ProjectTransferProgress> progress)
        {
            _skippedDirectoryCount++;
            string name = string.IsNullOrWhiteSpace(relativeDirectory) ? Name : relativeDirectory;
            progress?.Report(new ProjectTransferProgress
            {
                Operation = "Importing Resource Folder",
                Message = $"Skipped {name}: {exception.Message}",
                CompletedItems = _scannedFileCount
            });
        }

        private static bool IsInDirectoryTree(string relativePath, string relativeDirectory)
        {
            string normalizedPath = ResourceFolderPath.Normalize(relativePath);
            string normalizedDirectory = ResourceFolderPath.Normalize(relativeDirectory);
            return normalizedPath.StartsWith(normalizedDirectory + "/", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class ResourceFolderPath
    {
        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string[] parts = path
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(part => part != ".")
                .ToArray();
            return string.Join('/', parts);
        }

        public static string Combine(string directory, string path)
        {
            string normalizedDirectory = Normalize(directory);
            string normalizedPath = Normalize(path);
            if (string.IsNullOrWhiteSpace(normalizedDirectory))
            {
                return normalizedPath;
            }

            return string.IsNullOrWhiteSpace(normalizedPath)
                ? normalizedDirectory
                : normalizedDirectory + "/" + normalizedPath;
        }

        public static string GetDirectoryName(string path)
        {
            string normalized = Normalize(path);
            int slash = normalized.LastIndexOf('/');
            return slash < 0 ? string.Empty : normalized[..slash];
        }

        public static string GetFileName(string path)
        {
            string normalized = Normalize(path);
            int slash = normalized.LastIndexOf('/');
            return slash < 0 ? normalized : normalized[(slash + 1)..];
        }

        public static string GetFileNameWithoutExtension(string path)
        {
            return Path.GetFileNameWithoutExtension(GetFileName(path));
        }

        public static string GetExtension(string path)
        {
            return Path.GetExtension(GetFileName(path));
        }

        public static bool HasExtension(string path)
        {
            return !string.IsNullOrEmpty(GetExtension(path));
        }

        public static bool IsSafeRelativePath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return false;
            }

            return Normalize(path)
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .All(part => part != "..");
        }
    }
}
