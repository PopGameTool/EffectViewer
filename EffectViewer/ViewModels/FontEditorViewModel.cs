using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using EffectViewer.Assets;
using EffectViewer.Localization;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;
using EffectViewer.Runtime;
using System.Threading.Tasks;
using System.Numerics;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Graphics;
using TodFont = EffectViewer.TodLib.Graphics.Font;
using TodImage = EffectViewer.TodLib.Graphics.Image;

namespace EffectViewer.ViewModels
{
    public sealed partial class FontEditorViewModel : EditorViewModelBase
    {
        private readonly EffectProject _project;
        private const int MaxUndoHistoryCount = 100;
        private const int DefaultTrueTypeFontSize = 32;
        private const int MaxTrueTypeFontSize = 256;
        private const int MaxTrueTypeBorderSize = 64;
        private const string EnglishPreviewText = "ABC abc 123";
        private const string ChinesePreviewText = "中文汉字";
        private readonly List<FontEditorHistorySnapshot> _undoStack = [];
        private readonly List<FontEditorHistorySnapshot> _redoStack = [];
        private bool _isRestoringHistory;
        private TodFont _previewFont;
        private int _savedFontSize;
        private int _savedBorderSize;

        public FontAsset Asset { get; }

        [ObservableProperty]
        private string _assetId;

        private string _savedAssetId;

        public string SavedAssetId => _savedAssetId;
        public string Path => Asset.Path;
        public string FullPath => ProjectPathUtility.ResolvePath(_project, Asset.Path);
        public ObservableCollection<FontLayerViewModel> Layers { get; } = [];
        public ObservableCollection<FontTexturePreviewViewModel> Textures { get; } = [];

        [ObservableProperty]
        private FontTexturePreviewViewModel _selectedTexture;

        [ObservableProperty]
        private string _loadError = string.Empty;

        [ObservableProperty]
        private int _fontSize;

        [ObservableProperty]
        private int _borderSize;

        public int DefaultPointSize { get; private set; }
        public int PointSize { get; private set; }
        public float Ascent { get; private set; }
        public float Height { get; private set; }
        public float AscentPadding { get; private set; }
        public float LineSpacingOffset { get; private set; }
        public int LayerCount => Layers.Count;
        public int TextureCount => IsTrueType && _previewFont?.IsTrueType == true ? 1 : Textures.Count;
        public int GlyphCount { get; private set; }
        public string PreviewText { get; private set; } = string.Empty;
        public bool IsTrueType => Asset.TrueType || string.Equals(System.IO.Path.GetExtension(Asset.Path), ".ttf", StringComparison.OrdinalIgnoreCase);
        public bool IsImageFont => !IsTrueType;
        public bool HasTexturePreview => IsImageFont && HasTextures;
        public bool HasNoTextureMaps => IsImageFont && !HasTextures;
        public bool HasViewportPreview => IsTrueType || HasTexturePreview;
        public bool HasTextures => Textures.Count > 0;
        public bool HasNoTextures => !HasTextures;
        public bool HasMultipleTextures => Textures.Count > 1;
        public bool HasSelectedTexture => SelectedTexture is not null;
        public bool HasLoadError => !string.IsNullOrWhiteSpace(LoadError);
        public override bool SupportsSave => true;
        public override bool SavesWithProjectManifest => true;
        public override bool SupportsFileExport => true;
        public override string ExportPath => Path;
        public override bool CanUndo => _undoStack.Count > 0;
        public override bool CanRedo => _redoStack.Count > 0;

        public FontEditorViewModel(FontAsset asset, EffectProject project)
            : base(asset.Id, EffectAssetKind.Font)
        {
            _project = project;
            Asset = asset;
            if (IsTrueType)
            {
                Asset.TrueType = true;
                Asset.FontSize = Math.Clamp(Asset.FontSize <= 0 ? DefaultTrueTypeFontSize : Asset.FontSize, 1, MaxTrueTypeFontSize);
                Asset.BorderSize = Math.Clamp(Asset.BorderSize, 0, MaxTrueTypeBorderSize);
            }

            _assetId = asset.Id;
            _savedAssetId = asset.Id;
            _fontSize = IsTrueType ? Asset.FontSize : 0;
            _borderSize = IsTrueType ? Asset.BorderSize : 0;
            _savedFontSize = _fontSize;
            _savedBorderSize = _borderSize;
            TextureSource = new ProjectTextureSource(project);
            LoadFontPreview();
            RefreshPreviewFrame();
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
                string uniqueId = CreateUniqueFontAssetId(normalizedId);
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
                LoadFontPreview();
                RefreshPreviewFrame();
                OnPropertyChanged(nameof(AssetId));
            }
        }

        partial void OnSelectedTextureChanged(FontTexturePreviewViewModel value)
        {
            RefreshPreviewFrame();
            OnPropertyChanged(nameof(HasSelectedTexture));
        }

        partial void OnLoadErrorChanged(string value)
        {
            OnPropertyChanged(nameof(HasLoadError));
        }

        partial void OnFontSizeChanged(int value)
        {
            ApplyTrueTypeMetricChange(value, BorderSize);
        }

        partial void OnBorderSizeChanged(int value)
        {
            ApplyTrueTypeMetricChange(FontSize, value);
        }

        public override async Task SaveAsync(EffectProjectService projectService, EffectProject project)
        {
            if (!ApplyPendingAssetId())
            {
                throw new InvalidOperationException(LocalizationManager.Instance.Text("FontEditor.FontIdEmpty"));
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

            EnsureUniqueFontId(requestedId);
            bool idChanged = !string.Equals(Asset.Id, requestedId, StringComparison.Ordinal);
            if (idChanged)
            {
                Asset.Id = requestedId;
                Title = requestedId;
                DocumentId = CreateDocumentId(Kind, requestedId);
                _project?.RebuildAssetIndex();
                LoadFontPreview();
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
            _savedFontSize = Asset.FontSize;
            _savedBorderSize = Asset.BorderSize;
            base.AcceptSavedState();
        }

        public override void DiscardChanges()
        {
            RestoreHistorySnapshot(new FontEditorHistorySnapshot(_savedAssetId, _savedFontSize, _savedBorderSize));
            ClearUndoRedoHistory();
            base.DiscardChanges();
        }

        public override void Undo()
        {
            if (!CanUndo)
            {
                return;
            }

            FontEditorHistorySnapshot current = CreateCurrentHistorySnapshot();
            FontEditorHistorySnapshot previous = PopHistorySnapshot(_undoStack);
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

            FontEditorHistorySnapshot current = CreateCurrentHistorySnapshot();
            FontEditorHistorySnapshot next = PopHistorySnapshot(_redoStack);
            PushHistorySnapshot(_undoStack, current);
            RestoreHistorySnapshot(next);
            MarkDirty();
            RaiseUndoRedoStateChanged();
        }

        private void LoadFontPreview()
        {
            Layers.Clear();
            Textures.Clear();
            SelectedTexture = null;
            LoadError = string.Empty;
            PreviewText = string.Empty;
            _previewFont?.Dispose();
            _previewFont = null;

            string fullPath = FullPath;
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                LoadError = LocalizationManager.Instance.Format("FontEditor.DescriptorNotFound", fullPath);
                NotifyPreviewProperties();
                return;
            }

            try
            {
                ProjectResourceProvider provider = new(_project);
                TodFont font = provider.GetFont(Asset.Id);
                if (font is null)
                {
                    LoadError = LocalizationManager.Instance.Text("FontEditor.DescriptorCouldNotBeRead");
                    NotifyPreviewProperties();
                    return;
                }

                DefaultPointSize = font.mDefaultPointSize;
                PointSize = font.mPointSize;
                Ascent = font.mAscent;
                Height = font.mHeight;
                AscentPadding = font.mAscentPadding;
                LineSpacingOffset = font.mLineSpacingOffset;
                _previewFont = font;

                if (font.IsTrueType)
                {
                    GlyphCount = font.TrueTypeGlyphCount;
                    PreviewText = CreateTrueTypePreviewText(font);
                    NotifyMetricProperties();
                    NotifyPreviewProperties();
                    return;
                }

                HashSet<char> glyphs = [];
                HashSet<string> textureIds = new(StringComparer.OrdinalIgnoreCase);
                foreach (var layer in font.Layers)
                {
                    foreach (var item in layer.CharData)
                    {
                        glyphs.Add(item.Key);
                    }

                    TodImage image = layer.Image;
                    string imageId = image?.mId ?? string.Empty;
                    int characterCount = layer.CharData.Count();
                    Layers.Add(new FontLayerViewModel(
                        layer.Name,
                        layer.ImageName,
                        imageId,
                        layer.PointSize,
                        layer.Ascent,
                        layer.Height,
                        layer.Spacing,
                        characterCount,
                        layer.DrawMode));

                    if (!string.IsNullOrWhiteSpace(imageId) && textureIds.Add(imageId))
                    {
                        Textures.Add(new FontTexturePreviewViewModel(
                            layer.Name,
                            layer.ImageName,
                            imageId,
                            ResolveImagePath(imageId),
                            image?.mWidth ?? 0,
                            image?.mHeight ?? 0));
                    }
                }

                GlyphCount = glyphs.Count;
                SelectedTexture = Textures.FirstOrDefault();
                NotifyMetricProperties();
                NotifyPreviewProperties();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException or ArgumentException)
            {
                LoadError = LocalizationManager.Instance.Format("FontEditor.DescriptorLoadFailed", ex.Message);
                NotifyPreviewProperties();
            }
        }

        private string ResolveImagePath(string imageId)
        {
            return _project?.Assets?.Images?.TryGetValue(imageId, out ImageAsset image) == true
                ? image.Path
                : string.Empty;
        }

        private void RefreshPreviewFrame()
        {
            PreviewFrame = IsTrueType
                ? BuildTrueTypePreviewFrame()
                : SelectedTexture?.PreviewFrame ?? EffectPreviewFrameBuilder.BuildPlaceholder(EffectAssetKind.Font, AssetId);
        }

        private void NotifyMetricProperties()
        {
            OnPropertyChanged(nameof(DefaultPointSize));
            OnPropertyChanged(nameof(PointSize));
            OnPropertyChanged(nameof(Ascent));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(AscentPadding));
            OnPropertyChanged(nameof(LineSpacingOffset));
            OnPropertyChanged(nameof(GlyphCount));
            OnPropertyChanged(nameof(PreviewText));
        }

        private void NotifyPreviewProperties()
        {
            OnPropertyChanged(nameof(IsTrueType));
            OnPropertyChanged(nameof(IsImageFont));
            OnPropertyChanged(nameof(LayerCount));
            OnPropertyChanged(nameof(TextureCount));
            OnPropertyChanged(nameof(HasTextures));
            OnPropertyChanged(nameof(HasNoTextures));
            OnPropertyChanged(nameof(HasTexturePreview));
            OnPropertyChanged(nameof(HasNoTextureMaps));
            OnPropertyChanged(nameof(HasViewportPreview));
            OnPropertyChanged(nameof(HasMultipleTextures));
        }

        private void RecordUndoSnapshot()
        {
            if (_isRestoringHistory)
            {
                return;
            }

            FontEditorHistorySnapshot snapshot = CreateAssetHistorySnapshot();
            if (_undoStack.Count > 0 && _undoStack[^1].Equals(snapshot))
            {
                return;
            }

            PushHistorySnapshot(_undoStack, snapshot);
            _redoStack.Clear();
            RaiseUndoRedoStateChanged();
        }

        private void RestoreHistorySnapshot(FontEditorHistorySnapshot snapshot)
        {
            try
            {
                _isRestoringHistory = true;
                Asset.Id = snapshot.AssetId;
                Asset.FontSize = snapshot.FontSize;
                Asset.BorderSize = snapshot.BorderSize;
                AssetId = snapshot.AssetId;
                FontSize = IsTrueType ? snapshot.FontSize : 0;
                BorderSize = IsTrueType ? snapshot.BorderSize : 0;
                Title = snapshot.AssetId;
                DocumentId = CreateDocumentId(Kind, snapshot.AssetId);
                _project?.RebuildAssetIndex();
                LoadFontPreview();
                RefreshPreviewFrame();
            }
            finally
            {
                _isRestoringHistory = false;
            }
        }

        private FontEditorHistorySnapshot CreateAssetHistorySnapshot()
        {
            return new FontEditorHistorySnapshot(Asset.Id, Asset.FontSize, Asset.BorderSize);
        }

        private FontEditorHistorySnapshot CreateCurrentHistorySnapshot()
        {
            return new FontEditorHistorySnapshot(Asset.Id, Asset.FontSize, Asset.BorderSize);
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

        private static void PushHistorySnapshot(List<FontEditorHistorySnapshot> stack, FontEditorHistorySnapshot snapshot)
        {
            stack.Add(snapshot);
            if (stack.Count > MaxUndoHistoryCount)
            {
                stack.RemoveAt(0);
            }
        }

        private static FontEditorHistorySnapshot PopHistorySnapshot(List<FontEditorHistorySnapshot> stack)
        {
            int index = stack.Count - 1;
            FontEditorHistorySnapshot snapshot = stack[index];
            stack.RemoveAt(index);
            return snapshot;
        }

        private string CreateUniqueFontAssetId(string assetId)
        {
            string candidate = assetId;
            for (int i = 2; FontIdExists(candidate); i++)
            {
                candidate = $"{assetId}_{i}";
            }

            return candidate;
        }

        private void EnsureUniqueFontId(string assetId)
        {
            if (FontIdExists(assetId))
            {
                throw new InvalidOperationException(LocalizationManager.Instance.Format("FontEditor.FontIdExists", assetId));
            }
        }

        private bool FontIdExists(string assetId)
        {
            return _project?.Manifest?.Fonts?.Any(asset =>
                !ReferenceEquals(asset, Asset) &&
                string.Equals(asset.Id, assetId, StringComparison.OrdinalIgnoreCase)) == true;
        }

        private static string NormalizeEditedAssetId(string assetId)
        {
            return string.IsNullOrWhiteSpace(assetId)
                ? string.Empty
                : assetId.Trim();
        }

        private void ApplyTrueTypeMetricChange(int requestedFontSize, int requestedBorderSize)
        {
            if (_isRestoringHistory || !IsTrueType)
            {
                return;
            }

            int clampedFontSize = Math.Clamp(requestedFontSize <= 0 ? DefaultTrueTypeFontSize : requestedFontSize, 1, MaxTrueTypeFontSize);
            int clampedBorderSize = Math.Clamp(requestedBorderSize, 0, MaxTrueTypeBorderSize);
            if (FontSize != clampedFontSize)
            {
                FontSize = clampedFontSize;
                return;
            }

            if (BorderSize != clampedBorderSize)
            {
                BorderSize = clampedBorderSize;
                return;
            }

            if (Asset.FontSize == clampedFontSize && Asset.BorderSize == clampedBorderSize)
            {
                return;
            }

            RecordUndoSnapshot();
            Asset.FontSize = clampedFontSize;
            Asset.BorderSize = clampedBorderSize;
            LoadFontPreview();
            RefreshPreviewFrame();
            MarkDirty();
        }

        private RenderFrame BuildTrueTypePreviewFrame()
        {
            if (_previewFont is null || !_previewFont.IsTrueType)
            {
                return EffectPreviewFrameBuilder.BuildPlaceholder(EffectAssetKind.Font, AssetId);
            }

            FrameCaptureGraphics graphics = new()
            {
                mDrawMode = DrawMode.Normal,
                mColor = SexyColor.White,
                mClipRect = new System.Drawing.Rectangle(-8192, -8192, 16384, 16384)
            };

            DrawPreviewLine(graphics, _previewFont, EnglishPreviewText, 32f, 32f + _previewFont.mAscent);
            if (_previewFont.SupportsChinese)
            {
                DrawPreviewLine(graphics, _previewFont, ChinesePreviewText, 32f, 32f + _previewFont.mAscent + _previewFont.mHeight + 18f);
            }

            return graphics.Frame;
        }

        private static void DrawPreviewLine(FrameCaptureGraphics graphics, TodFont font, string text, float x, float baselineY)
        {
            Matrix4x4 matrix = Matrix4x4.Identity;
            TodCommon.SexyMatrix3Translation(ref matrix, x, baselineY);
            TodCommon.TodDrawStringMatrix(graphics, font, matrix, text, SexyColor.White);
        }

        private static string CreateTrueTypePreviewText(TodFont font)
        {
            return font?.SupportsChinese == true
                ? EnglishPreviewText + " / " + ChinesePreviewText
                : EnglishPreviewText;
        }

        public override void Dispose()
        {
            _previewFont?.Dispose();
            _previewFont = null;
            base.Dispose();
        }

        private readonly record struct FontEditorHistorySnapshot(string AssetId, int FontSize, int BorderSize);
    }
}
