using CommunityToolkit.Mvvm.ComponentModel;
using EffectViewer.Assets;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Math = System.Math;

namespace EffectViewer.ViewModels
{
    public sealed partial class ImageEditorViewModel : EditorViewModelBase
    {
        private readonly EffectProject _project;

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
            Rows = value < 1 ? 1 : value;
            Asset.Rows = Rows;
            MarkDirty();
            ClampFrameIndex();
            RefreshPreviewFrame();
            NotifyCellProperties();
        }

        partial void OnColsChanged(int value)
        {
            Cols = value < 1 ? 1 : value;
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
                throw new InvalidOperationException("Image ID cannot be empty.");
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
            AssetId = _savedAssetId;
            Asset.Id = _savedAssetId;
            Title = _savedAssetId;
            DocumentId = CreateDocumentId(Kind, _savedAssetId);
            Rows = _savedRows;
            Cols = _savedCols;
            _project?.RebuildAssetIndex();
            Asset.Rows = _savedRows;
            Asset.Cols = _savedCols;
            ClampFrameIndex();
            RefreshPreviewFrame();
            NotifyCellProperties();
            base.DiscardChanges();
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
                throw new InvalidOperationException($"Image ID '{assetId}' already exists.");
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
    }
}
