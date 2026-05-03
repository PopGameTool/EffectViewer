using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Styling;
using System.Numerics;
using EffectViewer.Rendering;
using EffectViewer.Rendering.Gl;
using EffectViewer.Rendering.OpenGl;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.Controls
{
    public sealed class OpenGlEffectViewport : OpenGlControlBase, IEffectViewport
    {
        public static readonly StyledProperty<RenderFrame> FrameProperty =
            AvaloniaProperty.Register<OpenGlEffectViewport, RenderFrame>(nameof(Frame), new RenderFrame());

        public static readonly StyledProperty<ITextureSource> TextureSourceProperty =
            AvaloniaProperty.Register<OpenGlEffectViewport, ITextureSource>(nameof(TextureSource));

        public static readonly StyledProperty<IRenderFrameProvider> FrameProviderProperty =
            AvaloniaProperty.Register<OpenGlEffectViewport, IRenderFrameProvider>(nameof(FrameProvider));

        public static readonly StyledProperty<ViewportBackgroundMode> BackgroundModeProperty =
            AvaloniaProperty.Register<OpenGlEffectViewport, ViewportBackgroundMode>(nameof(BackgroundMode));

        public static IEffectGlInterfaceFactory GlInterfaceFactory { get; set; } =
            new ProcAddressEffectGlInterfaceFactory(EffectGlApi.OpenGl, "OpenGL");

        private const double CheckerboardCellSize = 12d;
        private static readonly Vector4 DarkClearColor = new(0.08f, 0.09f, 0.1f, 1f);
        private static readonly Vector4 LightCheckerboardBaseColor = new(0.965f, 0.973f, 0.984f, 1f);
        private static readonly Vector4 LightCheckerboardAlternateColor = new(0.84f, 0.86f, 0.89f, 1f);
        private readonly OpenGlRenderer _renderer = new();
        private DateTime _lastRenderUtc = DateTime.UtcNow;
        private Vector2 _panPixels = Vector2.Zero;
        private float _zoom = 1f;

        public RenderFrame Frame
        {
            get => GetValue(FrameProperty);
            set => SetValue(FrameProperty, value);
        }

        public ITextureSource TextureSource
        {
            get => GetValue(TextureSourceProperty);
            set => SetValue(TextureSourceProperty, value);
        }

        public IRenderFrameProvider FrameProvider
        {
            get => GetValue(FrameProviderProperty);
            set => SetValue(FrameProviderProperty, value);
        }

        public ViewportBackgroundMode BackgroundMode
        {
            get => GetValue(BackgroundModeProperty);
            set => SetValue(BackgroundModeProperty, value);
        }

        public void SetViewTransform(float zoom, Vector2 panPixels)
        {
            _zoom = Math.Clamp(zoom, 0.05f, 32f);
            _panPixels = panPixels;
            RequestNextFrameRendering();
        }

        protected override void OnOpenGlInit(GlInterface gl)
        {
            base.OnOpenGlInit(gl);
            _renderer.Initialize(GlInterfaceFactory.Create(gl));
        }

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            if (TextureSource != null && !ReferenceEquals(_renderer.TextureSource, TextureSource))
            {
                _renderer.TextureSource = TextureSource;
            }

            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            PixelSize size = PixelSize.FromSize(Bounds.Size, scaling);
            DateTime now = DateTime.UtcNow;
            double deltaSeconds = Math.Clamp((now - _lastRenderUtc).TotalSeconds, 0, 0.1);
            _lastRenderUtc = now;
            RenderFrame frame = FrameProvider?.GetFrame(deltaSeconds) ?? Frame ?? new RenderFrame();
            _renderer.ViewZoom = _zoom;
            _renderer.ViewPan = _panPixels;
            _renderer.Render(
                frame,
                fb,
                size.Width,
                size.Height,
                GetBackgroundClearColor(),
                GetCheckerboardColor(),
                GetCheckerboardCellSize(scaling));
            RequestNextFrameRendering();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BackgroundModeProperty ||
                string.Equals(change.Property.Name, nameof(ActualThemeVariant), StringComparison.Ordinal))
            {
                RequestNextFrameRendering();
            }
        }

        protected override void OnOpenGlDeinit(GlInterface gl)
        {
            _renderer.Deinitialize();
            base.OnOpenGlDeinit(gl);
        }

        protected override void OnOpenGlLost()
        {
            _renderer.Deinitialize();
            base.OnOpenGlLost();
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
    }
}
