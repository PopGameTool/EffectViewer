using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using EffectViewer.Controls;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.Browser
{
    public sealed class BrowserAvaloniaWebGlEffectViewport : Control, IEffectViewport
    {
        private readonly BrowserWebGlFrameRenderer _renderer = new();
        private JSObject? _canvas;
        private WriteableBitmap? _bitmap;
        private byte[] _pixelBuffer = [];
        private RenderFrame _frame = new();
        private IRenderFrameProvider? _frameProvider;
        private DateTime _lastRenderUtc = DateTime.UtcNow;
        private bool _isAttached;
        private bool _frameQueued;

        public BrowserAvaloniaWebGlEffectViewport()
        {
            ClipToBounds = true;
        }

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
            get => _renderer.TextureSource;
            set
            {
                _renderer.TextureSource = value;
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
            get => _renderer.BackgroundMode;
            set
            {
                if (_renderer.BackgroundMode == value)
                {
                    return;
                }

                _renderer.BackgroundMode = value;
                QueueRenderFrame();
            }
        }

        public void SetViewTransform(float zoom, Vector2 panPixels)
        {
            _renderer.SetViewTransform(zoom, panPixels);
            QueueRenderFrame();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_bitmap is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return;
            }

            context.DrawImage(_bitmap, new Rect(Bounds.Size));
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _isAttached = true;
            _lastRenderUtc = DateTime.UtcNow;
            EnsureCanvas();
            QueueRenderFrame();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isAttached = false;
            DestroyCanvas();
            DisposeBitmap();
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

        private void EnsureCanvas()
        {
            if (_canvas is not null)
            {
                return;
            }

            _canvas = BrowserWebGlInterop.CreateViewport();
            _renderer.AttachCanvas(_canvas);
        }

        private void DestroyCanvas()
        {
            if (_canvas is null)
            {
                _renderer.DetachCanvas();
                return;
            }

            BrowserWebGlInterop.DestroyViewport(_canvas);
            _canvas = null;
            _renderer.DetachCanvas();
            _pixelBuffer = [];
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
            if (_canvas is null || !IsVisible || Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return;
            }

            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d;
            PixelSize pixelSize = PixelSize.FromSize(Bounds.Size, scaling);
            int width = Math.Max(1, pixelSize.Width);
            int height = Math.Max(1, pixelSize.Height);
            if (!TryGetRgbaByteCount(width, height, out int byteCount))
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            double deltaSeconds = Math.Clamp((now - _lastRenderUtc).TotalSeconds, 0d, 0.1d);
            _lastRenderUtc = now;

            RenderFrame frame = _frameProvider?.GetFrame(deltaSeconds) ?? _frame ?? new RenderFrame();
            _renderer.ActualThemeVariant = ActualThemeVariant;
            _renderer.Render(frame, width, height, scaling);

            EnsurePixelBuffer(byteCount);
            bool pixelsRead = BrowserWebGlInterop.ReadPixels(
                _canvas,
                new ArraySegment<byte>(_pixelBuffer, 0, byteCount),
                byteCount);
            if (!pixelsRead)
            {
                return;
            }

            PublishBitmap(width, height, byteCount);
            InvalidateVisual();
        }

        private void EnsurePixelBuffer(int byteCount)
        {
            if (_pixelBuffer.Length != byteCount)
            {
                _pixelBuffer = new byte[byteCount];
            }
        }

        private void PublishBitmap(int width, int height, int byteCount)
        {
            GCHandle handle = default;
            try
            {
                handle = GCHandle.Alloc(_pixelBuffer, GCHandleType.Pinned);
                WriteableBitmap next = new(
                    PixelFormat.Rgba8888,
                    AlphaFormat.Premul,
                    handle.AddrOfPinnedObject(),
                    new PixelSize(width, height),
                    new Avalonia.Vector(96, 96),
                    width * 4);
                WriteableBitmap? previous = _bitmap;
                _bitmap = next;
                previous?.Dispose();
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }

        private void DisposeBitmap()
        {
            _bitmap?.Dispose();
            _bitmap = null;
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
    }
}
