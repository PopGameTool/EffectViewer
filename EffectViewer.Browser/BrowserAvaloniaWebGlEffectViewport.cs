using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Styling;
using Avalonia.VisualTree;
using EffectViewer.Controls;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.Browser;

public sealed class BrowserAvaloniaWebGlEffectViewport : Control, IEffectViewport
{
    private BrowserWebGlRenderSession? _session;
    private RenderFrame _frame = new();
    private ITextureSource _textureSource = new GeneratedTextureSource();
    private IRenderFrameProvider? _frameProvider;
    private ViewportBackgroundMode _backgroundMode;
    private Vector2 _panPixels;
    private float _zoom = 1f;
    private DateTime _lastRenderUtc = DateTime.UtcNow;
    private bool _frameQueued;
    private int _attachmentVersion;

    public BrowserAvaloniaWebGlEffectViewport() => ClipToBounds = true;

    public RenderFrame Frame
    {
        get => _frame;
        set { _frame = value ?? new RenderFrame(); InvalidateVisual(); }
    }
    public ITextureSource TextureSource
    {
        get => _textureSource;
        set { _textureSource = value ?? new GeneratedTextureSource(); InvalidateVisual(); }
    }
    public IRenderFrameProvider FrameProvider
    {
        get => _frameProvider!;
        set { _frameProvider = value; InvalidateVisual(); }
    }
    public ViewportBackgroundMode BackgroundMode
    {
        get => _backgroundMode;
        set { _backgroundMode = value; InvalidateVisual(); }
    }
    public void SetViewTransform(float zoom, Vector2 panPixels)
    {
        _zoom = Math.Clamp(zoom, 0.05f, 32f);
        _panPixels = panPixels;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_session is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d;
        PixelSize size = PixelSize.FromSize(Bounds.Size, scaling);
        size = new PixelSize(Math.Max(1, size.Width), Math.Max(1, size.Height));
        DateTime now = DateTime.UtcNow;
        double delta = Math.Clamp((now - _lastRenderUtc).TotalSeconds, 0d, 0.1d);
        _lastRenderUtc = now;
        RenderFrame source = _frameProvider?.GetFrame(delta) ?? _frame;
        // Providers reuse buffers; recorded operations must own a stable snapshot.
        RenderFrame snapshot = new() { ClearColor = source.ClearColor };
        snapshot.Sprites.AddRange(source.Sprites);
        foreach (RenderMeshCommand mesh in source.Meshes)
            snapshot.AddMesh(mesh.Texture, source.GetMeshVertices(mesh), mesh.BlendMode);
        bool light = BackgroundMode == ViewportBackgroundMode.Light ||
            (BackgroundMode != ViewportBackgroundMode.Dark && ActualThemeVariant == ThemeVariant.Light);
        context.Custom(new DrawOperation(_session, snapshot, TextureSource, size, new Rect(Bounds.Size),
            _zoom, _panPixels,
            light ? new Vector4(0.965f, 0.973f, 0.984f, 1f) : new Vector4(0.08f, 0.09f, 0.1f, 1f),
            light ? new Vector4(0.84f, 0.86f, 0.89f, 1f) : null,
            Math.Max(4, (int)Math.Round(12d * scaling))));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _session = new BrowserWebGlRenderSession();
        _attachmentVersion++;
        _lastRenderUtc = DateTime.UtcNow;
        QueueRenderFrame();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attachmentVersion++;
        _frameQueued = false;
        _session?.Release();
        _session = null;
        base.OnDetachedFromVisualTree(e);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty || change.Property == IsVisibleProperty ||
            change.Property.Name == nameof(ActualThemeVariant))
            InvalidateVisual();
    }
    private void QueueRenderFrame()
    {
        if (_session is null || _frameQueued || TopLevel.GetTopLevel(this) is not { } topLevel)
            return;
        _frameQueued = true;
        int attachment = _attachmentVersion;
        topLevel.RequestAnimationFrame(_ =>
        {
            // Ignore callbacks left over from an earlier attachment.
            if (attachment != _attachmentVersion)
                return;
            _frameQueued = false;
            if (_session is null)
                return;
            if (IsVisible)
                InvalidateVisual();
            QueueRenderFrame();
        });
    }

    private sealed class DrawOperation : ICustomDrawOperation
    {
        private BrowserWebGlRenderSession? _session;
        private readonly RenderFrame _frame;
        private readonly ITextureSource _textures;
        private readonly PixelSize _size;
        private readonly float _zoom;
        private readonly Vector2 _pan;
        private readonly Vector4 _clear;
        private readonly Vector4? _checkerboard;
        private readonly int _cellSize;

        public DrawOperation(BrowserWebGlRenderSession session, RenderFrame frame, ITextureSource textures,
            PixelSize size, Rect bounds, float zoom, Vector2 pan, Vector4 clear, Vector4? checkerboard, int cellSize)
        {
            _session = session;
            session.Retain();
            _frame = frame;
            _textures = textures;
            _size = size;
            Bounds = bounds;
            _zoom = zoom;
            _pan = pan;
            _clear = clear;
            _checkerboard = checkerboard;
            _cellSize = cellSize;
        }
        public Rect Bounds { get; }
        public bool HitTest(Point p) => Bounds.Contains(p);
        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);
        public void Render(ImmediateDrawingContext context)
        {
            if (_session is null)
                return;
            ISkiaSharpApiLeaseFeature feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()
                ?? throw new InvalidOperationException("Effect preview requires the Skia renderer.");
            using ISkiaSharpApiLease lease = feature.Lease();
            _session.Draw(lease, _frame, _textures, _size, Bounds, _zoom, _pan, _clear, _checkerboard, _cellSize);
        }
        public void Dispose()
        {
            _session?.Release();
            _session = null;
        }
    }
}
