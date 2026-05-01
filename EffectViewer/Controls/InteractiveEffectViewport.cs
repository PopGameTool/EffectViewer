using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;

namespace EffectViewer.Controls
{
    public sealed class InteractiveEffectViewport : UserControl
    {
        public static readonly StyledProperty<RenderFrame> FrameProperty =
            AvaloniaProperty.Register<InteractiveEffectViewport, RenderFrame>(nameof(Frame), new RenderFrame());

        public static readonly StyledProperty<ITextureSource> TextureSourceProperty =
            AvaloniaProperty.Register<InteractiveEffectViewport, ITextureSource>(nameof(TextureSource));

        public static readonly StyledProperty<IRenderFrameProvider> FrameProviderProperty =
            AvaloniaProperty.Register<InteractiveEffectViewport, IRenderFrameProvider>(nameof(FrameProvider));

        public static readonly StyledProperty<IViewportDragHandler> DragHandlerProperty =
            AvaloniaProperty.Register<InteractiveEffectViewport, IViewportDragHandler>(nameof(DragHandler));

        public static Func<Control> ViewportFactory { get; set; } = static () => new OpenGlEffectViewport();

        private readonly Control _viewportControl;
        private readonly IEffectViewport _viewport;
        private Vector2 _panPixels;
        private Vector2 _lastPanPositionPixels;
        private float _zoom = 1f;
        private bool _isPanning;
        private bool _isDraggingContent;

        public InteractiveEffectViewport()
        {
            Focusable = true;
            ClipToBounds = true;
            Background = Brushes.Transparent;

            _viewportControl = ViewportFactory?.Invoke() ?? new OpenGlEffectViewport();
            if (_viewportControl is not IEffectViewport viewport)
            {
                throw new InvalidOperationException($"{nameof(ViewportFactory)} must create a control that implements {nameof(IEffectViewport)}.");
            }

            _viewport = viewport;

            Grid root = new();
            _viewportControl.IsHitTestVisible = false;
            root.Children.Add(_viewportControl);
            Content = root;
        }

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

        public IViewportDragHandler DragHandler
        {
            get => GetValue(DragHandlerProperty);
            set => SetValue(DragHandlerProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == FrameProperty)
            {
                _viewport.Frame = Frame;
            }
            else if (change.Property == TextureSourceProperty)
            {
                _viewport.TextureSource = TextureSource;
            }
            else if (change.Property == FrameProviderProperty)
            {
                _viewport.FrameProvider = FrameProvider;
            }
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            Vector2 cursorPixels = ToPixels(e.GetPosition(this), scaling);
            float oldZoom = _zoom;
            float factor = (float)Math.Pow(1.12, e.Delta.Y);
            float newZoom = Math.Clamp(oldZoom * factor, 0.05f, 32f);
            if (Math.Abs(newZoom - oldZoom) < 0.0001f)
            {
                return;
            }

            Vector2 worldUnderCursor = (cursorPixels - _panPixels) / oldZoom;
            _zoom = newZoom;
            _panPixels = cursorPixels - worldUnderCursor * _zoom;
            ApplyViewTransform();
            e.Handled = true;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            PointerPoint point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed && !point.Properties.IsMiddleButtonPressed)
            {
                return;
            }

            Focus();
            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            _lastPanPositionPixels = ToPixels(e.GetPosition(this), scaling);
            bool forcePan = point.Properties.IsMiddleButtonPressed || DragHandler?.IsPanModeEnabled == true;
            _isDraggingContent = point.Properties.IsLeftButtonPressed && !forcePan && DragHandler?.CanDrag == true;
            _isPanning = forcePan || !_isDraggingContent;
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!_isPanning && !_isDraggingContent)
            {
                return;
            }

            PointerPoint point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed && !point.Properties.IsMiddleButtonPressed)
            {
                EndPointerAction(e.Pointer);
                return;
            }

            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            Vector2 positionPixels = ToPixels(e.GetPosition(this), scaling);
            Vector2 deltaPixels = positionPixels - _lastPanPositionPixels;
            if (_isDraggingContent)
            {
                DragHandler?.DragBy(deltaPixels / _zoom);
            }
            else
            {
                _panPixels += deltaPixels;
                ApplyViewTransform();
            }

            _lastPanPositionPixels = positionPixels;
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            EndPointerAction(e.Pointer);
            e.Handled = true;
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            _isPanning = false;
            _isDraggingContent = false;
        }

        protected override void OnDoubleTapped(TappedEventArgs e)
        {
            base.OnDoubleTapped(e);
            _zoom = 1f;
            _panPixels = Vector2.Zero;
            ApplyViewTransform();
            e.Handled = true;
        }

        private void ApplyViewTransform()
        {
            _viewport.SetViewTransform(_zoom, _panPixels);
        }

        private void EndPointerAction(IPointer pointer)
        {
            if (!_isPanning && !_isDraggingContent)
            {
                return;
            }

            _isPanning = false;
            _isDraggingContent = false;
            pointer.Capture(null);
        }

        private static Vector2 ToPixels(Point point, double scaling)
        {
            return new Vector2((float)(point.X * scaling), (float)(point.Y * scaling));
        }
    }
}
