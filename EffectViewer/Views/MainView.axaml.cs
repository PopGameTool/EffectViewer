using Avalonia.Controls;
using Avalonia.Platform.Storage;
using EffectViewer.ViewModels;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace EffectViewer.Views
{
    public partial class MainView : UserControl
    {
        private const double DefaultProjectExplorerWidth = 280d;
        private const double MinimumProjectExplorerWidth = 180d;
        private const double SplitterWidth = 5d;
        private double _lastLeftProjectExplorerWidth = DefaultProjectExplorerWidth;
        private double _lastRightProjectExplorerWidth = DefaultProjectExplorerWidth;
        private MainViewModel _observedViewModel;

        public MainView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.EventArgs e)
        {
            if (_observedViewModel is not null)
            {
                _observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _observedViewModel = DataContext as MainViewModel;
            if (_observedViewModel is not null)
            {
                _observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            }

            UpdateProjectExplorerLayout();
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainViewModel.IsProjectExplorerVisibleLeft)
                or nameof(MainViewModel.IsProjectExplorerVisibleRight)
                or nameof(MainViewModel.IsProjectExplorerVisible))
            {
                UpdateProjectExplorerLayout();
            }
            else if (e.PropertyName == nameof(MainViewModel.LayoutResetRevision))
            {
                ResetProjectExplorerWidths();
                UpdateProjectExplorerLayout(captureCurrentWidth: false);
            }
        }

        private void CaptureVisibleProjectExplorerWidth()
        {
            ColumnDefinition leftColumn = WorkspaceGrid.ColumnDefinitions[0];
            ColumnDefinition rightColumn = WorkspaceGrid.ColumnDefinitions[4];

            if (_observedViewModel?.IsProjectExplorerVisibleLeft == true && leftColumn.ActualWidth > 0)
            {
                _lastLeftProjectExplorerWidth = leftColumn.ActualWidth;
            }
            else if (_observedViewModel?.IsProjectExplorerVisibleRight == true && rightColumn.ActualWidth > 0)
            {
                _lastRightProjectExplorerWidth = rightColumn.ActualWidth;
            }
        }

        private void UpdateProjectExplorerLayout(bool captureCurrentWidth = true)
        {
            if (captureCurrentWidth)
            {
                CaptureVisibleProjectExplorerWidth();
            }

            bool showLeft = _observedViewModel?.IsProjectExplorerVisibleLeft == true;
            bool showRight = _observedViewModel?.IsProjectExplorerVisibleRight == true;

            ConfigureColumn(
                WorkspaceGrid.ColumnDefinitions[0],
                showLeft ? _lastLeftProjectExplorerWidth : 0d,
                showLeft ? MinimumProjectExplorerWidth : 0d);
            ConfigureColumn(
                WorkspaceGrid.ColumnDefinitions[1],
                showLeft ? SplitterWidth : 0d,
                showLeft ? SplitterWidth : 0d);
            ConfigureColumn(
                WorkspaceGrid.ColumnDefinitions[3],
                showRight ? SplitterWidth : 0d,
                showRight ? SplitterWidth : 0d);
            ConfigureColumn(
                WorkspaceGrid.ColumnDefinitions[4],
                showRight ? _lastRightProjectExplorerWidth : 0d,
                showRight ? MinimumProjectExplorerWidth : 0d);
        }

        private void ResetProjectExplorerWidths()
        {
            _lastLeftProjectExplorerWidth = DefaultProjectExplorerWidth;
            _lastRightProjectExplorerWidth = DefaultProjectExplorerWidth;
        }

        private static void ConfigureColumn(ColumnDefinition column, double width, double minWidth)
        {
            column.MinWidth = minWidth;
            column.Width = new GridLength(width, GridUnitType.Pixel);
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
            IReadOnlyList<string> exportPatterns = viewModel.GetSelectedFileExportPatterns();
            List<FilePickerFileType> fileTypeChoices = exportPatterns.Count == 2
                ?
                [
                    new FilePickerFileType("Source") { Patterns = [exportPatterns[0]] },
                    new FilePickerFileType("Compiled") { Patterns = [exportPatterns[1]] }
                ]
                :
                [
                    new FilePickerFileType("Supported formats") { Patterns = [.. exportPatterns] }
                ];
            IStorageFile file = await topLevel.StorageProvider.SaveFilePickerAsync(new()
            {
                Title = "Export current file",
                SuggestedFileName = suggestedName,
                DefaultExtension = defaultExtension,
                FileTypeChoices = fileTypeChoices
            });

            if (file is null)
            {
                return;
            }

            await using Stream stream = await file.OpenWriteAsync();
            await viewModel.ExportSelectedFileAsync(stream, file.Name);
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
