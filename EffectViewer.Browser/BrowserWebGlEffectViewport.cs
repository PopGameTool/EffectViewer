using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using EffectViewer.Controls;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.Browser
{
    public sealed class BrowserWebGlEffectViewport : NativeControlHost, IEffectViewport
    {
        private const int FloatsPerVertex = 8;
        private static readonly float[] EmptyVertices = [];
        private static readonly byte[] EmptyVertexBytes = [];
        private static readonly int[] EmptyIntArray = [];
        private static readonly string[] EmptyStringArray = [];
        private static readonly RenderTextureRef WhiteTexture = new(FrameCaptureGraphics.WhiteTextureId);
        private const double CheckerboardCellSize = 12d;
        private static readonly Vector4 DarkClearColor = new(0.08f, 0.09f, 0.1f, 1f);
        private static readonly Vector4 LightCheckerboardBaseColor = new(0.965f, 0.973f, 0.984f, 1f);
        private static readonly Vector4 LightCheckerboardAlternateColor = new(0.84f, 0.86f, 0.89f, 1f);

        private readonly List<float> _vertices = [];
        private readonly List<int> _batchFirstVertices = [];
        private readonly List<int> _batchVertexCounts = [];
        private readonly List<int> _batchBlendModes = [];
        private readonly List<string> _batchTextureIds = [];
        private readonly Dictionary<string, UploadedTexture> _uploadedTextures = new(StringComparer.Ordinal);

        private JSObject? _canvas;
        private RenderFrame _frame = new();
        private ITextureSource _textureSource = new GeneratedTextureSource();
        private IRenderFrameProvider? _frameProvider;
        private DateTime _lastRenderUtc = DateTime.UtcNow;
        private Vector2 _panPixels = Vector2.Zero;
        private float _zoom = 1f;
        private ViewportBackgroundMode _backgroundMode;
        private bool _isAttached;
        private bool _frameQueued;
        private int _maxTextureSize;

        public RenderFrame Frame
        {
            get => _frame;
            set
            {
                _frame = value ?? new RenderFrame();
                QueueRenderFrame();
            }
        }

        public ITextureSource TextureSource
        {
            get => _textureSource;
            set
            {
                ITextureSource next = value ?? new GeneratedTextureSource();
                if (ReferenceEquals(_textureSource, next))
                {
                    return;
                }

                _textureSource = next;
                _uploadedTextures.Clear();
                if (_canvas is not null)
                {
                    BrowserWebGlInterop.ClearTextures(_canvas);
                }

                QueueRenderFrame();
            }
        }

        public IRenderFrameProvider FrameProvider
        {
            get => _frameProvider!;
            set
            {
                _frameProvider = value;
                QueueRenderFrame();
            }
        }

        public ViewportBackgroundMode BackgroundMode
        {
            get => _backgroundMode;
            set
            {
                if (_backgroundMode == value)
                {
                    return;
                }

                _backgroundMode = value;
                QueueRenderFrame();
            }
        }

        public void SetViewTransform(float zoom, Vector2 panPixels)
        {
            _zoom = Math.Clamp(zoom, 0.05f, 32f);
            _panPixels = panPixels;
            QueueRenderFrame();
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            _canvas = BrowserWebGlInterop.CreateViewport();
            return new JSObjectControlHandle(_canvas);
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            if (_canvas is not null)
            {
                BrowserWebGlInterop.DestroyViewport(_canvas);
                _canvas = null;
            }

            _uploadedTextures.Clear();
            _maxTextureSize = 0;
            base.DestroyNativeControlCore(control);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _isAttached = true;
            _lastRenderUtc = DateTime.UtcNow;
            QueueRenderFrame();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isAttached = false;
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BoundsProperty ||
                change.Property == IsVisibleProperty ||
                string.Equals(change.Property.Name, nameof(ActualThemeVariant), StringComparison.Ordinal))
            {
                QueueRenderFrame();
            }
        }

        private void QueueRenderFrame()
        {
            if (!_isAttached || _frameQueued)
            {
                return;
            }

            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
            {
                return;
            }

            _frameQueued = true;
            topLevel.RequestAnimationFrame(_ =>
            {
                _frameQueued = false;
                RenderNow();

                if (_isAttached)
                {
                    QueueRenderFrame();
                }
            });
        }

        private void RenderNow()
        {
            if (_canvas is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return;
            }

            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d;
            PixelSize pixelSize = PixelSize.FromSize(Bounds.Size, scaling);
            int width = Math.Max(1, pixelSize.Width);
            int height = Math.Max(1, pixelSize.Height);

            DateTime now = DateTime.UtcNow;
            double deltaSeconds = Math.Clamp((now - _lastRenderUtc).TotalSeconds, 0d, 0.1d);
            _lastRenderUtc = now;

            RenderFrame frame = _frameProvider?.GetFrame(deltaSeconds) ?? _frame ?? new RenderFrame();
            Vector4 clear = GetBackgroundClearColor();
            BuildDrawData(
                frame,
                width,
                height,
                GetCheckerboardColor(),
                GetCheckerboardCellSize(scaling));

            float[] vertices = _vertices.Count == 0 ? EmptyVertices : _vertices.ToArray();
            byte[] vertexBytes = ToByteArray(vertices);
            int[] batchFirstVertices = _batchFirstVertices.Count == 0 ? EmptyIntArray : _batchFirstVertices.ToArray();
            int[] batchVertexCounts = _batchVertexCounts.Count == 0 ? EmptyIntArray : _batchVertexCounts.ToArray();
            int[] batchBlendModes = _batchBlendModes.Count == 0 ? EmptyIntArray : _batchBlendModes.ToArray();
            string[] batchTextureIds = _batchTextureIds.Count == 0 ? EmptyStringArray : _batchTextureIds.ToArray();

            BrowserWebGlInterop.RenderFrame(
                _canvas,
                width,
                height,
                clear.X,
                clear.Y,
                clear.Z,
                clear.W,
                new ArraySegment<byte>(vertexBytes, 0, vertexBytes.Length),
                vertexBytes.Length,
                batchFirstVertices,
                batchVertexCounts,
                batchBlendModes,
                batchTextureIds);
        }

        private Vector4 GetBackgroundClearColor()
        {
            return ShouldUseLightBackground() ? LightCheckerboardBaseColor : DarkClearColor;
        }

        private Vector4? GetCheckerboardColor()
        {
            return ShouldUseLightBackground() ? LightCheckerboardAlternateColor : null;
        }

        private bool ShouldUseLightBackground()
        {
            if (BackgroundMode == ViewportBackgroundMode.Light)
            {
                return true;
            }

            if (BackgroundMode == ViewportBackgroundMode.Dark)
            {
                return false;
            }

            return ActualThemeVariant == ThemeVariant.Light;
        }

        private static int GetCheckerboardCellSize(double scaling)
        {
            return Math.Max(4, (int)Math.Round(CheckerboardCellSize * scaling));
        }

        private static byte[] ToByteArray(float[] vertices)
        {
            if (vertices.Length == 0)
            {
                return EmptyVertexBytes;
            }

            byte[] bytes = new byte[vertices.Length * sizeof(float)];
            Buffer.BlockCopy(vertices, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private void BuildDrawData(
            RenderFrame frame,
            int width,
            int height,
            Vector4? checkerboardColor,
            int checkerboardCellSizePixels)
        {
            _vertices.Clear();
            _batchFirstVertices.Clear();
            _batchVertexCounts.Clear();
            _batchBlendModes.Clear();
            _batchTextureIds.Clear();

            if (checkerboardColor.HasValue && EnsureTexture(WhiteTexture, out TextureTileLayout whiteLayout))
            {
                int firstVertex = _vertices.Count / FloatsPerVertex;
                int vertexCount = AppendCheckerboard(width, height, checkerboardColor.Value, checkerboardCellSizePixels);
                if (vertexCount > 0)
                {
                    AddBatch(
                        firstVertex,
                        vertexCount,
                        RenderBlendMode.Normal,
                        TextureTileLayout.GetTileTextureId(WhiteTexture.Id, whiteLayout.Tiles[0]));
                }
            }

            foreach (RenderSpriteCommand sprite in frame.Sprites)
            {
                if (!EnsureTexture(sprite.Texture, out TextureTileLayout layout))
                {
                    continue;
                }

                if (layout.IsTiled)
                {
                    AppendTiledTriangles(
                        CreateSpriteVertices(sprite, width, height),
                        layout,
                        sprite.BlendMode,
                        sprite.Texture);
                }
                else
                {
                    int firstVertex = _vertices.Count / FloatsPerVertex;
                    AppendSprite(sprite, width, height);
                    AddBatch(
                        firstVertex,
                        6,
                        sprite.BlendMode,
                        TextureTileLayout.GetTileTextureId(sprite.Texture.Id, layout.Tiles[0]));
                }
            }

            foreach (RenderMeshCommand mesh in frame.Meshes)
            {
                if (mesh.Vertices.Count == 0 || !EnsureTexture(mesh.Texture, out TextureTileLayout layout))
                {
                    continue;
                }

                if (layout.IsTiled)
                {
                    List<RenderVertex> vertices = new(mesh.Vertices.Count);
                    foreach (RenderVertex vertex in mesh.Vertices)
                    {
                        vertices.Add(new RenderVertex(
                            new Vector2(
                                ToClipX(ApplyViewX(vertex.Position.X), width),
                                ToClipY(ApplyViewY(vertex.Position.Y), height)),
                            vertex.Uv,
                            vertex.Color));
                    }

                    AppendTiledTriangles(vertices, layout, mesh.BlendMode, mesh.Texture);
                }
                else
                {
                    int firstVertex = _vertices.Count / FloatsPerVertex;
                    foreach (RenderVertex vertex in mesh.Vertices)
                    {
                        AppendVertex(
                            ToClipX(ApplyViewX(vertex.Position.X), width),
                            ToClipY(ApplyViewY(vertex.Position.Y), height),
                            vertex.Uv.X,
                            vertex.Uv.Y,
                            vertex.Color);
                    }

                    AddBatch(
                        firstVertex,
                        mesh.Vertices.Count,
                        mesh.BlendMode,
                        TextureTileLayout.GetTileTextureId(mesh.Texture.Id, layout.Tiles[0]));
                }
            }
        }

        private int AppendCheckerboard(int width, int height, Vector4 alternateColor, int cellSizePixels)
        {
            int cellSize = Math.Max(4, cellSizePixels);
            int columns = Math.Max(1, (width + cellSize - 1) / cellSize);
            int rows = Math.Max(1, (height + cellSize - 1) / cellSize);
            int startVertexCount = _vertices.Count / FloatsPerVertex;

            for (int row = 0; row < rows; row++)
            {
                int top = row * cellSize;
                int bottom = Math.Min(height, top + cellSize);
                for (int column = 0; column < columns; column++)
                {
                    if (((row + column) & 1) == 0)
                    {
                        continue;
                    }

                    int left = column * cellSize;
                    int right = Math.Min(width, left + cellSize);
                    AppendScreenRect(left, top, right, bottom, width, height, alternateColor);
                }
            }

            return _vertices.Count / FloatsPerVertex - startVertexCount;
        }

        private bool EnsureTexture(RenderTextureRef texture, out TextureTileLayout layout)
        {
            layout = null!;
            if (_canvas is null)
            {
                return false;
            }

            string id = texture.Id ?? string.Empty;
            int revision = GetTextureRevision(texture);
            if (_uploadedTextures.TryGetValue(id, out UploadedTexture? uploaded) && uploaded.Revision == revision)
            {
                layout = uploaded.Layout;
                return true;
            }

            if (!_textureSource.TryLoad(texture, out TextureUploadData data) ||
                data is null ||
                data.Width <= 0 ||
                data.Height <= 0 ||
                data.RgbaPixels is null ||
                !TryGetRgbaByteCount(data.Width, data.Height, out int sourceByteCount) ||
                data.RgbaPixels.Length < sourceByteCount)
            {
                return false;
            }

            layout = TextureTileLayout.Create(data.Width, data.Height, GetMaxTextureSize());
            foreach (TextureTile tile in layout.Tiles)
            {
                byte[] sourcePixels = layout.IsTiled
                    ? TextureTileLayout.CopyTilePixels(data, tile)
                    : data.RgbaPixels;
                if (!TryGetRgbaByteCount(tile.UploadWidth, tile.UploadHeight, out int byteCount) ||
                    sourcePixels.Length < byteCount)
                {
                    layout = null!;
                    return false;
                }

                bool tileUploaded = BrowserWebGlInterop.UploadTexture(
                    _canvas,
                    TextureTileLayout.GetTileTextureId(id, tile),
                    tile.UploadWidth,
                    tile.UploadHeight,
                    new ArraySegment<byte>(sourcePixels, 0, byteCount),
                    byteCount);

                if (!tileUploaded)
                {
                    layout = null!;
                    return false;
                }
            }

            _uploadedTextures[id] = new UploadedTexture(layout, revision);
            return true;
        }

        private int GetTextureRevision(RenderTextureRef texture)
        {
            return _textureSource is ITextureRevisionSource revisionSource
                ? revisionSource.GetTextureRevision(texture)
                : 0;
        }

        private int GetMaxTextureSize()
        {
            if (_maxTextureSize > 0)
            {
                return _maxTextureSize;
            }

            int maxTextureSize = _canvas is null ? 0 : BrowserWebGlInterop.GetMaxTextureSize(_canvas);
            _maxTextureSize = maxTextureSize > 0 ? maxTextureSize : 4096;
            return _maxTextureSize;
        }

        private static bool TryGetRgbaByteCount(int width, int height, out int byteCount)
        {
            try
            {
                byteCount = checked(width * height * 4);
                return true;
            }
            catch (OverflowException)
            {
                byteCount = 0;
                return false;
            }
        }

        private void AddBatch(int firstVertex, int vertexCount, RenderBlendMode blendMode, string textureId)
        {
            _batchFirstVertices.Add(firstVertex);
            _batchVertexCounts.Add(vertexCount);
            _batchBlendModes.Add(blendMode == RenderBlendMode.Additive ? 1 : 0);
            _batchTextureIds.Add(textureId ?? string.Empty);
        }

        private void AppendTiledTriangles(
            IReadOnlyList<RenderVertex> vertices,
            TextureTileLayout layout,
            RenderBlendMode blendMode,
            RenderTextureRef texture)
        {
            string id = texture.Id ?? string.Empty;
            foreach (TextureTileDrawBatch batch in TextureTileClipper.CreateBatches(layout, vertices))
            {
                if (batch.Vertices.Count == 0)
                {
                    continue;
                }

                int firstVertex = _vertices.Count / FloatsPerVertex;
                foreach (RenderVertex vertex in batch.Vertices)
                {
                    AppendVertex(
                        vertex.Position.X,
                        vertex.Position.Y,
                        vertex.Uv.X,
                        vertex.Uv.Y,
                        vertex.Color);
                }

                AddBatch(
                    firstVertex,
                    batch.Vertices.Count,
                    blendMode,
                    TextureTileLayout.GetTileTextureId(id, batch.Tile));
            }
        }

        private void AppendSprite(RenderSpriteCommand sprite, int width, int height)
        {
            foreach (RenderVertex vertex in CreateSpriteVertices(sprite, width, height))
            {
                AppendVertex(
                    vertex.Position.X,
                    vertex.Position.Y,
                    vertex.Uv.X,
                    vertex.Uv.Y,
                    vertex.Color);
            }
        }

        private RenderVertex[] CreateSpriteVertices(RenderSpriteCommand sprite, int width, int height)
        {
            float pixelX = sprite.Position.X <= 1f ? sprite.Position.X * width : sprite.Position.X;
            float pixelY = sprite.Position.Y <= 1f ? sprite.Position.Y * height : sprite.Position.Y;
            float pixelW = sprite.Size.X <= 1f ? sprite.Size.X * width : sprite.Size.X;
            float pixelH = sprite.Size.Y <= 1f ? sprite.Size.Y * height : sprite.Size.Y;

            float left = ToClipX(ApplyViewX(pixelX - pixelW / 2f), width);
            float right = ToClipX(ApplyViewX(pixelX + pixelW / 2f), width);
            float top = ToClipY(ApplyViewY(pixelY - pixelH / 2f), height);
            float bottom = ToClipY(ApplyViewY(pixelY + pixelH / 2f), height);
            Vector4 uv = sprite.UvRect;
            Vector4 color = sprite.Color;

            return
            [
                new RenderVertex(new Vector2(left, top), new Vector2(uv.X, uv.Y), color),
                new RenderVertex(new Vector2(right, top), new Vector2(uv.Z, uv.Y), color),
                new RenderVertex(new Vector2(right, bottom), new Vector2(uv.Z, uv.W), color),
                new RenderVertex(new Vector2(left, top), new Vector2(uv.X, uv.Y), color),
                new RenderVertex(new Vector2(right, bottom), new Vector2(uv.Z, uv.W), color),
                new RenderVertex(new Vector2(left, bottom), new Vector2(uv.X, uv.W), color)
            ];
        }

        private void AppendVertex(float x, float y, float u, float v, Vector4 color)
        {
            _vertices.Add(x);
            _vertices.Add(y);
            _vertices.Add(u);
            _vertices.Add(v);
            _vertices.Add(color.X);
            _vertices.Add(color.Y);
            _vertices.Add(color.Z);
            _vertices.Add(color.W);
        }

        private void AppendScreenRect(float left, float top, float right, float bottom, int width, int height, Vector4 color)
        {
            float clipLeft = ToClipX(left, width);
            float clipRight = ToClipX(right, width);
            float clipTop = ToClipY(top, height);
            float clipBottom = ToClipY(bottom, height);

            AppendVertex(clipLeft, clipTop, 0f, 0f, color);
            AppendVertex(clipRight, clipTop, 1f, 0f, color);
            AppendVertex(clipRight, clipBottom, 1f, 1f, color);
            AppendVertex(clipLeft, clipTop, 0f, 0f, color);
            AppendVertex(clipRight, clipBottom, 1f, 1f, color);
            AppendVertex(clipLeft, clipBottom, 0f, 1f, color);
        }

        private float ApplyViewX(float x)
        {
            return x * _zoom + _panPixels.X;
        }

        private float ApplyViewY(float y)
        {
            return y * _zoom + _panPixels.Y;
        }

        private static float ToClipX(float x, int width)
        {
            return width <= 0 ? 0f : x / width * 2f - 1f;
        }

        private static float ToClipY(float y, int height)
        {
            return height <= 0 ? 0f : 1f - y / height * 2f;
        }

        private sealed class UploadedTexture
        {
            public UploadedTexture(TextureTileLayout layout, int revision)
            {
                Layout = layout;
                Revision = revision;
            }

            public TextureTileLayout Layout { get; }
            public int Revision { get; }
        }
    }
}
