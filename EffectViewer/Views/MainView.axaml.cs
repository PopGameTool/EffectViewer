using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using EffectViewer.Localization;
using EffectViewer.ViewModels;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EffectViewer.Views
{
    public partial class MainView : UserControl
    {
        private const double DefaultProjectExplorerWidth = 280d;
        private const double MinimumProjectExplorerWidth = 180d;
        private const double SplitterWidth = 5d;
        private const double DocumentTabDragThreshold = 6d;
        private double _lastLeftProjectExplorerWidth = DefaultProjectExplorerWidth;
        private double _lastRightProjectExplorerWidth = DefaultProjectExplorerWidth;
        private Control _draggedDocumentTab;
        private EditorViewModelBase _draggedDocumentEditor;
        private EditorViewModelBase _documentTabDropTarget;
        private Point _documentTabDragStartPoint;
        private Point _documentTabDragPreviewOffset;
        private bool _documentTabDropAfter;
        private bool _isDocumentTabDragging;
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
                _observedViewModel.ImportResourceFolderRequested -= OnImportResourceFolderRequested;
            }

            _observedViewModel = DataContext as MainViewModel;
            if (_observedViewModel is not null)
            {
                _observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
                _observedViewModel.ImportResourceFolderRequested += OnImportResourceFolderRequested;
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

        private void DocumentTab_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is not Control tab ||
                tab.DataContext is not EditorViewModelBase editor ||
                !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
                IsCloseButtonPointer(e))
            {
                return;
            }

            if (DataContext is MainViewModel viewModel)
            {
                viewModel.SelectedEditor = editor;
            }

            _draggedDocumentTab = tab;
            _draggedDocumentEditor = editor;
            _documentTabDragStartPoint = e.GetPosition(this);
            _documentTabDragPreviewOffset = e.GetPosition(tab);
            _isDocumentTabDragging = false;
            e.Pointer.Capture(tab);
            e.Handled = true;
        }

        private void DocumentTab_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_draggedDocumentTab is null)
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                EndDocumentTabDrag(e);
                return;
            }

            Point currentPoint = e.GetPosition(this);
            if (!_isDocumentTabDragging &&
                GetDragDistance(_documentTabDragStartPoint, currentPoint) >= DocumentTabDragThreshold)
            {
                _isDocumentTabDragging = true;
                _draggedDocumentEditor.IsTabDragging = true;
                ShowDocumentTabDragPreview();
            }

            if (_isDocumentTabDragging)
            {
                UpdateDocumentTabDragPreviewPosition(currentPoint);
                UpdateDocumentTabDropTarget(e);
            }

            e.Handled = _isDocumentTabDragging;
        }

        private void DocumentTab_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_draggedDocumentTab is not null &&
                _isDocumentTabDragging &&
                DataContext is MainViewModel viewModel)
            {
                UpdateDocumentTabDropTarget(e);
                if (_documentTabDropTarget is not null)
                {
                    viewModel.ReorderEditorTab(_draggedDocumentEditor, _documentTabDropTarget, _documentTabDropAfter);
                }
            }

            EndDocumentTabDrag(e);
            e.Handled = true;
        }

        private void DocumentTab_PointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
        {
            if (_draggedDocumentTab is null)
            {
                return;
            }

            ClearDocumentTabDragVisuals();
            _draggedDocumentTab = null;
            _draggedDocumentEditor = null;
            _documentTabDropTarget = null;
            _documentTabDropAfter = false;
            _isDocumentTabDragging = false;
        }

        private void EndDocumentTabDrag(PointerEventArgs e)
        {
            e.Pointer.Capture(null);
            ClearDocumentTabDragVisuals();
            _draggedDocumentTab = null;
            _draggedDocumentEditor = null;
            _documentTabDropTarget = null;
            _documentTabDropAfter = false;
            _isDocumentTabDragging = false;
        }

        private void ShowDocumentTabDragPreview()
        {
            if (_draggedDocumentTab is null || _draggedDocumentEditor is null)
            {
                return;
            }

            DocumentTabDragPreview.Width = System.Math.Clamp(_draggedDocumentTab.Bounds.Width, 150d, 320d);
            DocumentTabDragPreview.Height = _draggedDocumentTab.Bounds.Height;
            DocumentTabDragPreviewKind.Text = _draggedDocumentEditor.KindCode;
            DocumentTabDragPreviewTitle.Text = _draggedDocumentEditor.TabTitle;
            DocumentTabDragPreviewPin.IsVisible = _draggedDocumentEditor.IsPinned;
            DocumentTabDragPreview.IsVisible = true;
            UpdateDocumentTabDragPreviewPosition(_documentTabDragStartPoint);
        }

        private void UpdateDocumentTabDragPreviewPosition(Point point)
        {
            if (!DocumentTabDragPreview.IsVisible)
            {
                return;
            }

            Canvas.SetLeft(DocumentTabDragPreview, point.X - _documentTabDragPreviewOffset.X);
            Canvas.SetTop(DocumentTabDragPreview, point.Y - _documentTabDragPreviewOffset.Y);
        }

        private void UpdateDocumentTabDropTarget(PointerEventArgs e)
        {
            Point point = e.GetPosition(this);
            if (TryFindDocumentTabAt(point, out Control targetTab) &&
                targetTab.DataContext is EditorViewModelBase targetEditor)
            {
                bool insertAfter = e.GetPosition(targetTab).X > targetTab.Bounds.Width / 2d;
                SetDocumentTabDropTarget(targetEditor, insertAfter);
                return;
            }

            if (TryFindDocumentTabStripEdgeTarget(point, out targetEditor, out bool insertAfterAtEdge))
            {
                SetDocumentTabDropTarget(targetEditor, insertAfterAtEdge);
                return;
            }

            ClearDocumentTabDropTarget();
        }

        private void SetDocumentTabDropTarget(EditorViewModelBase targetEditor, bool insertAfter)
        {
            if (ReferenceEquals(targetEditor, _draggedDocumentEditor))
            {
                ClearDocumentTabDropTarget();
                return;
            }

            if (!ReferenceEquals(_documentTabDropTarget, targetEditor))
            {
                ClearDocumentTabDropTarget();
                _documentTabDropTarget = targetEditor;
            }

            _documentTabDropAfter = insertAfter;
            targetEditor.IsTabDropBefore = !insertAfter;
            targetEditor.IsTabDropAfter = insertAfter;
        }

        private bool TryFindDocumentTabStripEdgeTarget(
            Point point,
            out EditorViewModelBase targetEditor,
            out bool insertAfter)
        {
            targetEditor = null;
            insertAfter = false;

            if (!IsPointInsideDocumentTabStrip(point))
            {
                return false;
            }

            List<DocumentTabBounds> tabs = GetDocumentTabBounds();
            if (tabs.Count == 0)
            {
                return false;
            }

            DocumentTabBounds first = tabs[0];
            DocumentTabBounds last = tabs[^1];
            if (point.X <= first.Left)
            {
                targetEditor = first.Editor;
                insertAfter = false;
                return true;
            }

            if (point.X >= last.Right)
            {
                targetEditor = last.Editor;
                insertAfter = true;
                return true;
            }

            for (int i = 0; i < tabs.Count - 1; i++)
            {
                DocumentTabBounds current = tabs[i];
                DocumentTabBounds next = tabs[i + 1];
                if (point.X >= current.Right && point.X <= next.Left)
                {
                    double midpoint = current.Right + ((next.Left - current.Right) / 2d);
                    if (point.X <= midpoint)
                    {
                        targetEditor = current.Editor;
                        insertAfter = true;
                    }
                    else
                    {
                        targetEditor = next.Editor;
                        insertAfter = false;
                    }

                    return true;
                }
            }

            return false;
        }

        private void ClearDocumentTabDragVisuals()
        {
            if (_draggedDocumentEditor is not null)
            {
                _draggedDocumentEditor.IsTabDragging = false;
            }

            DocumentTabDragPreview.IsVisible = false;
            ClearDocumentTabDropTarget();
        }

        private void ClearDocumentTabDropTarget()
        {
            if (_documentTabDropTarget is not null)
            {
                _documentTabDropTarget.IsTabDropBefore = false;
                _documentTabDropTarget.IsTabDropAfter = false;
                _documentTabDropTarget = null;
            }

            _documentTabDropAfter = false;
        }

        private bool TryFindDocumentTabAt(Point point, out Control tab)
        {
            foreach (Avalonia.Visual visual in this.GetVisualsAt(point))
            {
                tab = visual.GetSelfAndVisualAncestors()
                    .OfType<Control>()
                    .FirstOrDefault(control =>
                        control.Classes.Contains("document-tab") &&
                        control.DataContext is EditorViewModelBase);

                if (tab is not null)
                {
                    return true;
                }
            }

            tab = null;
            return false;
        }

        private bool IsPointInsideDocumentTabStrip(Point point)
        {
            Point? tabStripOrigin = DocumentTabHeaderGrid.TranslatePoint(new Point(0, 0), this);
            if (tabStripOrigin is null)
            {
                return false;
            }

            Rect tabStripBounds = new Rect(tabStripOrigin.Value, DocumentTabHeaderGrid.Bounds.Size);
            return tabStripBounds.Contains(point);
        }

        private List<DocumentTabBounds> GetDocumentTabBounds()
        {
            return DocumentTabStripScrollViewer
                .GetVisualDescendants()
                .OfType<Control>()
                .Where(control =>
                    control.Classes.Contains("document-tab") &&
                    control.DataContext is EditorViewModelBase &&
                    control.Bounds.Width > 0d)
                .Select(control =>
                {
                    Point? origin = control.TranslatePoint(new Point(0, 0), this);
                    EditorViewModelBase editor = (EditorViewModelBase)control.DataContext;
                    return origin is null
                        ? null
                        : new DocumentTabBounds(editor, origin.Value.X, origin.Value.X + control.Bounds.Width);
                })
                .Where(tab => tab is not null)
                .OrderBy(tab => tab.Left)
                .ToList();
        }

        private sealed class DocumentTabBounds
        {
            public DocumentTabBounds(EditorViewModelBase editor, double left, double right)
            {
                Editor = editor;
                Left = left;
                Right = right;
            }

            public EditorViewModelBase Editor { get; }

            public double Left { get; }

            public double Right { get; }
        }

        private static double GetDragDistance(Point start, Point current)
        {
            double deltaX = current.X - start.X;
            double deltaY = current.Y - start.Y;
            return System.Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        private static bool IsCloseButtonPointer(PointerPressedEventArgs e)
        {
            return e.Source is Avalonia.Visual visual &&
                visual.GetSelfAndVisualAncestors()
                    .OfType<Button>()
                    .Any(button => button.Classes.Contains("tab-close-button"));
        }

        private async void ImportFolderMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await ImportResourceFolderFromPickerAsync();
        }

        private async void OnImportResourceFolderRequested(object sender, System.EventArgs e)
        {
            await ImportResourceFolderFromPickerAsync();
        }

        private async Task ImportResourceFolderFromPickerAsync()
        {
            try
            {
                IStorageFolder folder = await PickFolderAsync(Loc.Text("FilePicker.ImportResourceFolder"));
                if (folder is not null && DataContext is MainViewModel viewModel)
                {
                    string path = folder.TryGetLocalPath();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        await viewModel.ImportResourceFolderAsync(path);
                    }
                    else
                    {
                        await viewModel.ImportResourceFolderAsync(folder);
                    }
                }
            }
            catch (System.Exception ex) when (DataContext is MainViewModel viewModel)
            {
                viewModel.StatusText = Loc.Format("Status.CouldNotImportFolder", ex.Message);
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
                Title = Loc.Text("FilePicker.ImportProjectZip"),
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

        private async void ImportResourceFileMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new()
            {
                Title = Loc.Text("FilePicker.ImportResourceFile"),
                AllowMultiple = false,
                FileTypeFilter = [ResourceFileType]
            });

            if (files.Count == 0)
            {
                return;
            }

            await using Stream stream = await files[0].OpenReadAsync();
            await viewModel.ImportResourceFileAsync(files[0].Name, stream);
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
                Title = Loc.Text("FilePicker.ExportProjectZip"),
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
                    new FilePickerFileType(Loc.Text("FilePicker.Source")) { Patterns = [exportPatterns[0]] },
                    new FilePickerFileType(Loc.Text("FilePicker.Compiled")) { Patterns = [exportPatterns[1]] }
                ]
                :
                [
                    new FilePickerFileType(Loc.Text("FilePicker.SupportedFormats")) { Patterns = [.. exportPatterns] }
                ];
            IStorageFile file = await topLevel.StorageProvider.SaveFilePickerAsync(new()
            {
                Title = Loc.Text("FilePicker.ExportCurrentFile"),
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

        private async void LoadLanguageFileMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new()
            {
                Title = Loc.Text("Menu.LoadLanguageFile"),
                AllowMultiple = false,
                FileTypeFilter = [LanguageFileType]
            });

            if (files.Count == 0)
            {
                return;
            }

            await using Stream stream = await files[0].OpenReadAsync();
            await viewModel.LoadLanguageFileAsync(stream, files[0].Name);
        }

        private async Task<IStorageFolder> PickFolderAsync(string title)
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

            return folders.Count > 0 ? folders[0] : null;
        }

        private static LocalizationManager Loc => LocalizationManager.Instance;

        private static FilePickerFileType ZipFileType => new(Loc.Text("FilePicker.ZipArchive"))
        {
            Patterns = ["*.zip"],
            MimeTypes = ["application/zip", "application/x-zip-compressed"]
        };

        private static FilePickerFileType ResourceFileType => new(Loc.Text("FilePicker.EffectResources"))
        {
            Patterns =
            [
                "*.png",
                "*.jpg",
                "*.jpeg",
                "*.bmp",
                "*.gif",
                "*.webp",
                "*.tga",
                "*.reanim",
                "*.reanim.compiled",
                "*.xml",
                "*.xml.compiled",
                "*.trail",
                "*.trail.compiled",
                "*.lua"
            ]
        };

        private static FilePickerFileType LanguageFileType => new(Loc.Text("FilePicker.JsonLanguageFiles"))
        {
            Patterns = ["*.json"],
            MimeTypes = ["application/json", "text/json"]
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
