using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Controls;
using Avalonia.Platform;
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

        private readonly List<float> _vertices = [];
        private readonly List<int> _batchFirstVertices = [];
        private readonly List<int> _batchVertexCounts = [];
        private readonly List<int> _batchBlendModes = [];
        private readonly List<string> _batchTextureIds = [];
        private readonly HashSet<string> _uploadedTextureIds = [];

        private JSObject? _canvas;
        private RenderFrame _frame = new();
        private ITextureSource _textureSource = new GeneratedTextureSource();
        private IRenderFrameProvider? _frameProvider;
        private DateTime _lastRenderUtc = DateTime.UtcNow;
        private Vector2 _panPixels = Vector2.Zero;
        private float _zoom = 1f;
        private bool _isAttached;
        private bool _frameQueued;

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
                _uploadedTextureIds.Clear();
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

            _uploadedTextureIds.Clear();
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
                change.Property == IsVisibleProperty)
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
            BuildDrawData(frame, width, height);

            float[] vertices = _vertices.Count == 0 ? EmptyVertices : _vertices.ToArray();
            byte[] vertexBytes = ToByteArray(vertices);
            int[] batchFirstVertices = _batchFirstVertices.Count == 0 ? EmptyIntArray : _batchFirstVertices.ToArray();
            int[] batchVertexCounts = _batchVertexCounts.Count == 0 ? EmptyIntArray : _batchVertexCounts.ToArray();
            int[] batchBlendModes = _batchBlendModes.Count == 0 ? EmptyIntArray : _batchBlendModes.ToArray();
            string[] batchTextureIds = _batchTextureIds.Count == 0 ? EmptyStringArray : _batchTextureIds.ToArray();
            Vector4 clear = frame.ClearColor;

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

        private void BuildDrawData(RenderFrame frame, int width, int height)
        {
            _vertices.Clear();
            _batchFirstVertices.Clear();
            _batchVertexCounts.Clear();
            _batchBlendModes.Clear();
            _batchTextureIds.Clear();

            foreach (RenderSpriteCommand sprite in frame.Sprites)
            {
                if (!EnsureTexture(sprite.Texture))
                {
                    continue;
                }

                int firstVertex = _vertices.Count / FloatsPerVertex;
                AppendSprite(sprite, width, height);
                AddBatch(firstVertex, 6, sprite.BlendMode, sprite.Texture);
            }

            foreach (RenderMeshCommand mesh in frame.Meshes)
            {
                if (mesh.Vertices.Count == 0 || !EnsureTexture(mesh.Texture))
                {
                    continue;
                }

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

                AddBatch(firstVertex, mesh.Vertices.Count, mesh.BlendMode, mesh.Texture);
            }
        }

        private bool EnsureTexture(RenderTextureRef texture)
        {
            if (_canvas is null)
            {
                return false;
            }

            string id = texture.Id ?? string.Empty;
            if (_uploadedTextureIds.Contains(id))
            {
                return true;
            }

            if (!_textureSource.TryLoad(texture, out TextureUploadData data) ||
                data is null ||
                data.Width <= 0 ||
                data.Height <= 0)
            {
                return false;
            }

            int byteCount = data.Width * data.Height * 4;
            if (data.RgbaPixels is null || data.RgbaPixels.Length < byteCount)
            {
                return false;
            }

            bool uploaded = BrowserWebGlInterop.UploadTexture(
                _canvas,
                id,
                data.Width,
                data.Height,
                new ArraySegment<byte>(data.RgbaPixels, 0, byteCount),
                byteCount);

            if (uploaded)
            {
                _uploadedTextureIds.Add(id);
            }

            return uploaded;
        }

        private void AddBatch(int firstVertex, int vertexCount, RenderBlendMode blendMode, RenderTextureRef texture)
        {
            _batchFirstVertices.Add(firstVertex);
            _batchVertexCounts.Add(vertexCount);
            _batchBlendModes.Add(blendMode == RenderBlendMode.Additive ? 1 : 0);
            _batchTextureIds.Add(texture.Id ?? string.Empty);
        }

        private void AppendSprite(RenderSpriteCommand sprite, int width, int height)
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

            AppendVertex(left, top, uv.X, uv.Y, color);
            AppendVertex(right, top, uv.Z, uv.Y, color);
            AppendVertex(right, bottom, uv.Z, uv.W, color);
            AppendVertex(left, top, uv.X, uv.Y, color);
            AppendVertex(right, bottom, uv.Z, uv.W, color);
            AppendVertex(left, bottom, uv.X, uv.W, color);
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
    }
}
