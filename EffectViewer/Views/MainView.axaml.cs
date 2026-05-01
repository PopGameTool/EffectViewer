using Avalonia.Controls;
using Avalonia.Platform.Storage;
using EffectViewer.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace EffectViewer.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }

        private async void ImportFolderMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string path = await PickFolderAsync("Import PopCap resource folder");
            if (!string.IsNullOrWhiteSpace(path) && DataContext is MainViewModel viewModel)
            {
                await viewModel.ImportResourceFolderAsync(path);
            }
        }

        private async void ImportProjectMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new()
            {
                Title = "Import EffectViewer project zip",
                AllowMultiple = false,
                FileTypeFilter = [ZipFileType]
            });

            if (files.Count == 0)
            {
                return;
            }

            await using Stream stream = await files[0].OpenReadAsync();
            await viewModel.ImportProjectZipAsync(stream);
        }

        private async void ExportProjectMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            string suggestedName = CreateExportFileName(viewModel.CurrentProject?.Manifest?.Name);
            IStorageFile file = await topLevel.StorageProvider.SaveFilePickerAsync(new()
            {
                Title = "Export EffectViewer project zip",
                SuggestedFileName = suggestedName,
                DefaultExtension = "zip",
                FileTypeChoices = [ZipFileType]
            });

            if (file is null)
            {
                return;
            }

            await using Stream stream = await file.OpenWriteAsync();
            await viewModel.ExportCurrentProjectZipAsync(stream);
        }

        private async void ExportFileMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            if (!await viewModel.PrepareSelectedFileExportAsync())
            {
                return;
            }

            string suggestedName = viewModel.GetSelectedFileExportName();
            string defaultExtension = viewModel.GetSelectedFileDefaultExtension();
            IStorageFile file = await topLevel.StorageProvider.SaveFilePickerAsync(new()
            {
                Title = "Export current file",
                SuggestedFileName = suggestedName,
                DefaultExtension = defaultExtension
            });

            if (file is null)
            {
                return;
            }

            await using Stream stream = await file.OpenWriteAsync();
            await viewModel.ExportSelectedFileAsync(stream);
        }

        private async Task<string> PickFolderAsync(string title)
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
            {
                return null;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new()
            {
                Title = title,
                AllowMultiple = false
            });

            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }

        private static FilePickerFileType ZipFileType { get; } = new("Zip archive")
        {
            Patterns = ["*.zip"],
            MimeTypes = ["application/zip", "application/x-zip-compressed"]
        };

        private static string CreateExportFileName(string projectName)
        {
            string name = string.IsNullOrWhiteSpace(projectName) ? "effectviewer-project" : projectName.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase)
                ? name
                : name + ".zip";
        }
    }
}
