using CommunityToolkit.Mvvm.ComponentModel;
using EffectViewer.Assets;
using EffectViewer.Localization;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Math = System.Math;

namespace EffectViewer.ViewModels
{
    public sealed partial class ImageEditorViewModel : EditorViewModelBase
    {
        private readonly EffectProject _project;
        private const int MaxUndoHistoryCount = 100;
        private readonly List<ImageEditorHistorySnapshot> _undoStack = [];
        private readonly List<ImageEditorHistorySnapshot> _redoStack = [];
        private bool _isRestoringHistory;

        public ImageAsset Asset { get; }

        [ObservableProperty]
        private string _assetId;

        [ObservableProperty]
        private int _rows;

        [ObservableProperty]
        private int _cols;

        [ObservableProperty]
        private int _frameIndex;
        private readonly int _imageWidth;
        private readonly int _imageHeight;
        private string _savedAssetId;
        private int _savedRows;
        private int _savedCols;

        public string SavedAssetId => _savedAssetId;
        public string Path => Asset.Path;
        public int CellCount => Rows * Cols;
        public int MaxFrameIndex => CellCount - 1;
        public int CurrentRow => Cols <= 0 ? 0 : FrameIndex / Cols;
        public int CurrentCol => Cols <= 0 ? 0 : FrameIndex % Cols;
        public override bool SupportsSave => true;
        public override bool SavesWithProjectManifest => true;
        public override bool SupportsFileExport => true;
        public override string ExportPath => Path;
        public override bool CanUndo => _undoStack.Count > 0;
        public override bool CanRedo => _redoStack.Count > 0;

        public ImageEditorViewModel(ImageAsset asset, EffectProject project)
            : base(asset.Id, EffectAssetKind.Image)
        {
            _project = project;
            Asset = asset;
            _assetId = asset.Id;
            _savedAssetId = asset.Id;
            _rows = asset.Rows;
            _cols = asset.Cols;
            _savedRows = asset.Rows;
            _savedCols = asset.Cols;
            _frameIndex = 0;
            ResolveImageSize(asset, project, out _imageWidth, out _imageHeight);
            RefreshPreviewFrame();
            TextureSource = new ProjectTextureSource(project);
        }

        partial void OnAssetIdChanged(string value)
        {
            if (_isRestoringHistory)
            {
                return;
            }

            string normalizedId = NormalizeEditedAssetId(value);
            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                normalizedId = Asset.Id;
            }

            if (!string.Equals(value, normalizedId, StringComparison.Ordinal))
            {
                AssetId = normalizedId;
                return;
            }

            if (!string.Equals(Asset.Id, normalizedId, StringComparison.Ordinal))
            {
                string uniqueId = CreateUniqueImageAssetId(normalizedId);
                if (!string.Equals(normalizedId, uniqueId, StringComparison.Ordinal))
                {
                    AssetId = uniqueId;
                    return;
                }

                RecordUndoSnapshot();
                Asset.Id = uniqueId;
                Title = uniqueId;
                DocumentId = CreateDocumentId(Kind, uniqueId);
                _project?.RebuildAssetIndex();
                MarkDirty();
                RefreshPreviewFrame();
                OnPropertyChanged(nameof(AssetId));
            }
        }

        private static string NormalizeEditedAssetId(string assetId)
        {
            return string.IsNullOrWhiteSpace(assetId)
                ? string.Empty
                : assetId.Trim();
        }

        partial void OnRowsChanged(int value)
        {
            if (_isRestoringHistory)
            {
                return;
            }

            if (value < 1)
            {
                Rows = 1;
                return;
            }

            if (Asset.Rows == value)
            {
                return;
            }

            RecordUndoSnapshot();
            Asset.Rows = Rows;
            MarkDirty();
            ClampFrameIndex();
            RefreshPreviewFrame();
            NotifyCellProperties();
        }

        partial void OnColsChanged(int value)
        {
            if (_isRestoringHistory)
            {
                return;
            }

            if (value < 1)
            {
                Cols = 1;
                return;
            }

            if (Asset.Cols == value)
            {
                return;
            }

            RecordUndoSnapshot();
            Asset.Cols = Cols;
            MarkDirty();
            ClampFrameIndex();
            RefreshPreviewFrame();
            NotifyCellProperties();
        }

        public override async Task SaveAsync(EffectProjectService projectService, EffectProject project)
        {
            if (!ApplyPendingAssetId())
            {
                throw new InvalidOperationException(LocalizationManager.Instance.Text("ImageEditor.ImageIdEmpty"));
            }

            await projectService.SaveAsync(project);
            AcceptSavedState();
        }

        public bool ApplyPendingAssetId()
        {
            string requestedId = NormalizeEditedAssetId(AssetId);
            if (string.IsNullOrWhiteSpace(requestedId))
            {
                return false;
            }

            EnsureUniqueImageId(requestedId);
            bool idChanged = !string.Equals(Asset.Id, requestedId, StringComparison.Ordinal);
            if (idChanged)
            {
                Asset.Id = requestedId;
                Title = requestedId;
                DocumentId = CreateDocumentId(Kind, requestedId);
                _project?.RebuildAssetIndex();
                RefreshPreviewFrame();
            }

            if (!string.Equals(AssetId, requestedId, StringComparison.Ordinal))
            {
                AssetId = requestedId;
            }

            return true;
        }

        public override void AcceptSavedState()
        {
            _savedAssetId = AssetId;
            _savedRows = Rows;
            _savedCols = Cols;
            base.AcceptSavedState();
        }

        public override void DiscardChanges()
        {
            RestoreHistorySnapshot(new ImageEditorHistorySnapshot(_savedAssetId, _savedRows, _savedCols, FrameIndex));
            ClearUndoRedoHistory();
            base.DiscardChanges();
        }

        public override void Undo()
        {
            if (!CanUndo)
            {
                return;
            }

            ImageEditorHistorySnapshot current = CreateCurrentHistorySnapshot();
            ImageEditorHistorySnapshot previous = PopHistorySnapshot(_undoStack);
            PushHistorySnapshot(_redoStack, current);
            RestoreHistorySnapshot(previous);
            MarkDirty();
            RaiseUndoRedoStateChanged();
        }

        public override void Redo()
        {
            if (!CanRedo)
            {
                return;
            }

            ImageEditorHistorySnapshot current = CreateCurrentHistorySnapshot();
            ImageEditorHistorySnapshot next = PopHistorySnapshot(_redoStack);
            PushHistorySnapshot(_undoStack, current);
            RestoreHistorySnapshot(next);
            MarkDirty();
            RaiseUndoRedoStateChanged();
        }

        partial void OnFrameIndexChanged(int value)
        {
            if (value < 0 || value > MaxFrameIndex)
            {
                FrameIndex = Math.Clamp(value, 0, MaxFrameIndex);
                return;
            }

            RefreshPreviewFrame();
            OnPropertyChanged(nameof(CurrentRow));
            OnPropertyChanged(nameof(CurrentCol));
        }

        private void RecordUndoSnapshot()
        {
            if (_isRestoringHistory)
            {
                return;
            }

            ImageEditorHistorySnapshot snapshot = CreateAssetHistorySnapshot();
            if (_undoStack.Count > 0 && _undoStack[^1].Equals(snapshot))
            {
                return;
            }

            PushHistorySnapshot(_undoStack, snapshot);
            _redoStack.Clear();
            RaiseUndoRedoStateChanged();
        }

        private void RestoreHistorySnapshot(ImageEditorHistorySnapshot snapshot)
        {
            try
            {
                _isRestoringHistory = true;
                Asset.Id = snapshot.AssetId;
                Asset.Rows = snapshot.Rows;
                Asset.Cols = snapshot.Cols;
                AssetId = snapshot.AssetId;
                Title = snapshot.AssetId;
                DocumentId = CreateDocumentId(Kind, snapshot.AssetId);
                Rows = snapshot.Rows;
                Cols = snapshot.Cols;
                _project?.RebuildAssetIndex();
                FrameIndex = Math.Clamp(snapshot.FrameIndex, 0, MaxFrameIndex);
                RefreshPreviewFrame();
                NotifyCellProperties();
            }
            finally
            {
                _isRestoringHistory = false;
            }
        }

        private ImageEditorHistorySnapshot CreateAssetHistorySnapshot()
        {
            return new ImageEditorHistorySnapshot(Asset.Id, Asset.Rows, Asset.Cols, FrameIndex);
        }

        private ImageEditorHistorySnapshot CreateCurrentHistorySnapshot()
        {
            return new ImageEditorHistorySnapshot(Asset.Id, Rows, Cols, FrameIndex);
        }

        private void ClearUndoRedoHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            RaiseUndoRedoStateChanged();
        }

        private void RaiseUndoRedoStateChanged()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        private static void PushHistorySnapshot(List<ImageEditorHistorySnapshot> stack, ImageEditorHistorySnapshot snapshot)
        {
            stack.Add(snapshot);
            if (stack.Count > MaxUndoHistoryCount)
            {
                stack.RemoveAt(0);
            }
        }

        private static ImageEditorHistorySnapshot PopHistorySnapshot(List<ImageEditorHistorySnapshot> stack)
        {
            int index = stack.Count - 1;
            ImageEditorHistorySnapshot snapshot = stack[index];
            stack.RemoveAt(index);
            return snapshot;
        }

        private string CreateUniqueImageAssetId(string assetId)
        {
            string candidate = assetId;
            for (int i = 2; ImageIdExists(candidate); i++)
            {
                candidate = $"{assetId}_{i}";
            }

            return candidate;
        }

        private void EnsureUniqueImageId(string assetId)
        {
            if (ImageIdExists(assetId))
            {
                throw new InvalidOperationException(LocalizationManager.Instance.Format("ImageEditor.ImageIdExists", assetId));
            }
        }

        private bool ImageIdExists(string assetId)
        {
            return _project?.Manifest?.Images?.Any(asset =>
                !ReferenceEquals(asset, Asset) &&
                string.Equals(asset.Id, assetId, StringComparison.OrdinalIgnoreCase)) == true;
        }

        private void ClampFrameIndex()
        {
            if (FrameIndex < 0)
            {
                FrameIndex = 0;
            }
            else if (FrameIndex > MaxFrameIndex)
            {
                FrameIndex = MaxFrameIndex;
            }
        }

        private void RefreshPreviewFrame()
        {
            PreviewFrame = EffectPreviewFrameBuilder.BuildImagePreview(Asset.Id, Rows, Cols, FrameIndex, _imageWidth, _imageHeight);
            OnPropertyChanged(nameof(PreviewFrame));
        }

        private static void ResolveImageSize(ImageAsset asset, EffectProject project, out int width, out int height)
        {
            width = 0;
            height = 0;
            string fullPath = System.IO.Path.IsPathRooted(asset.Path) || string.IsNullOrWhiteSpace(project.RootPath)
                ? asset.Path
                : System.IO.Path.Combine(project.RootPath, asset.Path);

            Runtime.ImageFileSizeReader.TryReadSize(fullPath, out width, out height);
        }

        private void NotifyCellProperties()
        {
            OnPropertyChanged(nameof(CellCount));
            OnPropertyChanged(nameof(MaxFrameIndex));
            OnPropertyChanged(nameof(CurrentRow));
            OnPropertyChanged(nameof(CurrentCol));
        }

        private readonly record struct ImageEditorHistorySnapshot(
            string AssetId,
            int Rows,
            int Cols,
            int FrameIndex);
    }
}
