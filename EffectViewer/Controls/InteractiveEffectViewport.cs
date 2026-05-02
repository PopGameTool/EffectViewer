using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;
using System.ComponentModel;

namespace EffectViewer.Controls
{
    public sealed class InteractiveEffectViewport : UserControl
    {
        private const double ScaleHandleSize = 10d;
        private const double SkewHandleSize = 8d;
        private const double HandleHitSize = 18d;

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
        private readonly TransformOverlay _overlay;
        private Vector2 _panPixels;
        private Vector2 _lastPanPositionPixels;
        private float _zoom = 1f;
        private bool _isPanning;
        private bool _isDraggingContent;
        private ViewportDragHandle _activeDragHandle;
        private IViewportDragHandler _subscribedDragHandler;

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
            _overlay = new TransformOverlay(this)
            {
                IsHitTestVisible = false
            };
            root.Children.Add(_overlay);
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
            else if (change.Property == DragHandlerProperty)
            {
                if (change.OldValue is IViewportDragHandler oldHandler)
                {
                    DetachDragHandler(oldHandler);
                }

                if (change.NewValue is IViewportDragHandler newHandler)
                {
                    AttachDragHandler(newHandler);
                }

                InvalidateOverlay();
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
            InvalidateOverlay();
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
            Vector2 worldPosition = ViewPixelsToWorld(_lastPanPositionPixels);
            _activeDragHandle = !forcePan && point.Properties.IsLeftButtonPressed
                ? HitTestTransformHandle(_lastPanPositionPixels)
                : ViewportDragHandle.None;
            _isDraggingContent = _activeDragHandle != ViewportDragHandle.None && DragHandler?.CanDrag == true;
            _isPanning = forcePan || !_isDraggingContent;
            if (_isDraggingContent)
            {
                DragHandler?.BeginDrag(_activeDragHandle, worldPosition);
            }

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
                DragHandler?.DragTo(ViewPixelsToWorld(positionPixels));
                InvalidateOverlay();
            }
            else
            {
                _panPixels += deltaPixels;
                ApplyViewTransform();
                InvalidateOverlay();
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
            _activeDragHandle = ViewportDragHandle.None;
            DragHandler?.EndDrag();
        }

        protected override void OnDoubleTapped(TappedEventArgs e)
        {
            base.OnDoubleTapped(e);
            _zoom = 1f;
            _panPixels = Vector2.Zero;
            ApplyViewTransform();
            InvalidateOverlay();
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
            _activeDragHandle = ViewportDragHandle.None;
            DragHandler?.EndDrag();
            pointer.Capture(null);
        }

        private void InvalidateOverlay()
        {
            InvalidateVisual();
            _overlay?.InvalidateVisual();
        }

        private void DrawTransformBox(DrawingContext context)
        {
            if (DragHandler?.CanDrag != true || DragHandler.TransformBox is not { } box)
            {
                return;
            }

            Point topLeft = WorldToViewPoint(box.TopLeft);
            Point topRight = WorldToViewPoint(box.TopRight);
            Point bottomRight = WorldToViewPoint(box.BottomRight);
            Point bottomLeft = WorldToViewPoint(box.BottomLeft);
            Point topCenter = WorldToViewPoint(box.TopCenter);
            Point rightCenter = WorldToViewPoint(box.RightCenter);
            Point bottomCenter = WorldToViewPoint(box.BottomCenter);
            Point leftCenter = WorldToViewPoint(box.LeftCenter);

            Pen outlinePen = new(new SolidColorBrush(Color.FromRgb(75, 155, 255)), 1.5);
            Pen shadowPen = new(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), 3);
            context.DrawLine(shadowPen, topLeft, topRight);
            context.DrawLine(shadowPen, topRight, bottomRight);
            context.DrawLine(shadowPen, bottomRight, bottomLeft);
            context.DrawLine(shadowPen, bottomLeft, topLeft);
            context.DrawLine(outlinePen, topLeft, topRight);
            context.DrawLine(outlinePen, topRight, bottomRight);
            context.DrawLine(outlinePen, bottomRight, bottomLeft);
            context.DrawLine(outlinePen, bottomLeft, topLeft);

            DrawScaleHandle(context, topLeft);
            DrawScaleHandle(context, topCenter);
            DrawScaleHandle(context, topRight);
            DrawScaleHandle(context, rightCenter);
            DrawScaleHandle(context, bottomRight);
            DrawScaleHandle(context, bottomCenter);
            DrawScaleHandle(context, bottomLeft);
            DrawScaleHandle(context, leftCenter);

            DrawSkewHandle(context, OffsetToward(topCenter, topLeft, 18d));
            DrawSkewHandle(context, OffsetToward(rightCenter, topRight, 18d));
            DrawSkewHandle(context, OffsetToward(bottomCenter, bottomRight, 18d));
            DrawSkewHandle(context, OffsetToward(leftCenter, bottomLeft, 18d));
        }

        private static void DrawScaleHandle(DrawingContext context, Point center)
        {
            Rect rect = CenteredRect(center, ScaleHandleSize, ScaleHandleSize);
            context.FillRectangle(Brushes.White, rect);
            context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromRgb(75, 155, 255)), 1), rect);
        }

        private static void DrawSkewHandle(DrawingContext context, Point center)
        {
            Point p1 = new(center.X, center.Y - SkewHandleSize * 0.65);
            Point p2 = new(center.X + SkewHandleSize * 0.65, center.Y);
            Point p3 = new(center.X, center.Y + SkewHandleSize * 0.65);
            Point p4 = new(center.X - SkewHandleSize * 0.65, center.Y);
            StreamGeometry geometry = new();
            using (StreamGeometryContext geometryContext = geometry.Open())
            {
                geometryContext.BeginFigure(p1, true);
                geometryContext.LineTo(p2);
                geometryContext.LineTo(p3);
                geometryContext.LineTo(p4);
                geometryContext.EndFigure(true);
            }

            context.DrawGeometry(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(255, 180, 58)), 1), geometry);
        }

        private ViewportDragHandle HitTestTransformHandle(Vector2 positionPixels)
        {
            if (DragHandler?.CanDrag != true || DragHandler.TransformBox is not { } box)
            {
                return ViewportDragHandle.None;
            }

            Point position = PixelsToViewPoint(positionPixels);
            Point topLeft = WorldToViewPoint(box.TopLeft);
            Point topRight = WorldToViewPoint(box.TopRight);
            Point bottomRight = WorldToViewPoint(box.BottomRight);
            Point bottomLeft = WorldToViewPoint(box.BottomLeft);
            Point topCenter = WorldToViewPoint(box.TopCenter);
            Point rightCenter = WorldToViewPoint(box.RightCenter);
            Point bottomCenter = WorldToViewPoint(box.BottomCenter);
            Point leftCenter = WorldToViewPoint(box.LeftCenter);

            if (HitHandle(position, topLeft)) return ViewportDragHandle.ScaleTopLeft;
            if (HitHandle(position, topCenter)) return ViewportDragHandle.ScaleTop;
            if (HitHandle(position, topRight)) return ViewportDragHandle.ScaleTopRight;
            if (HitHandle(position, rightCenter)) return ViewportDragHandle.ScaleRight;
            if (HitHandle(position, bottomRight)) return ViewportDragHandle.ScaleBottomRight;
            if (HitHandle(position, bottomCenter)) return ViewportDragHandle.ScaleBottom;
            if (HitHandle(position, bottomLeft)) return ViewportDragHandle.ScaleBottomLeft;
            if (HitHandle(position, leftCenter)) return ViewportDragHandle.ScaleLeft;

            if (HitHandle(position, OffsetToward(topCenter, topLeft, 18d))) return ViewportDragHandle.SkewTop;
            if (HitHandle(position, OffsetToward(rightCenter, topRight, 18d))) return ViewportDragHandle.SkewRight;
            if (HitHandle(position, OffsetToward(bottomCenter, bottomRight, 18d))) return ViewportDragHandle.SkewBottom;
            if (HitHandle(position, OffsetToward(leftCenter, bottomLeft, 18d))) return ViewportDragHandle.SkewLeft;

            return PointInQuad(position, topLeft, topRight, bottomRight, bottomLeft)
                ? ViewportDragHandle.Move
                : ViewportDragHandle.None;
        }

        private Point WorldToViewPoint(Vector2 worldPosition)
        {
            Vector2 point = worldPosition * _zoom + _panPixels;
            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d;
            return new Point(point.X / scaling, point.Y / scaling);
        }

        private Point PixelsToViewPoint(Vector2 positionPixels)
        {
            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d;
            return new Point(positionPixels.X / scaling, positionPixels.Y / scaling);
        }

        private Vector2 ViewPixelsToWorld(Vector2 positionPixels)
        {
            return (positionPixels - _panPixels) / _zoom;
        }

        private static bool HitHandle(Point position, Point center)
        {
            return CenteredRect(center, HandleHitSize, HandleHitSize).Contains(position);
        }

        private static Rect CenteredRect(Point center, double width, double height)
        {
            return new Rect(center.X - width * 0.5d, center.Y - height * 0.5d, width, height);
        }

        private static Point OffsetToward(Point origin, Point target, double distance)
        {
            Avalonia.Vector direction = target - origin;
            double length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
            if (length <= 0.001d)
            {
                return origin;
            }

            return origin + direction / length * distance;
        }

        private static bool PointInQuad(Point point, Point a, Point b, Point c, Point d)
        {
            return PointInTriangle(point, a, b, c) || PointInTriangle(point, a, c, d);
        }

        private static bool PointInTriangle(Point point, Point a, Point b, Point c)
        {
            double sign1 = Cross(point, a, b);
            double sign2 = Cross(point, b, c);
            double sign3 = Cross(point, c, a);
            bool hasNegative = sign1 < 0d || sign2 < 0d || sign3 < 0d;
            bool hasPositive = sign1 > 0d || sign2 > 0d || sign3 > 0d;
            return !(hasNegative && hasPositive);
        }

        private static double Cross(Point point, Point a, Point b)
        {
            return (point.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (point.Y - b.Y);
        }

        private void AttachDragHandler(IViewportDragHandler handler)
        {
            if (handler is null || ReferenceEquals(_subscribedDragHandler, handler))
            {
                return;
            }

            DetachDragHandler(_subscribedDragHandler);
            _subscribedDragHandler = handler;
            if (handler is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged += OnDragHandlerPropertyChanged;
            }
        }

        private void DetachDragHandler(IViewportDragHandler handler)
        {
            if (handler is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged -= OnDragHandlerPropertyChanged;
            }

            if (ReferenceEquals(_subscribedDragHandler, handler))
            {
                _subscribedDragHandler = null;
            }
        }

        private void OnDragHandlerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IViewportDragHandler.TransformBox) ||
                e.PropertyName == nameof(IViewportDragHandler.CanDrag) ||
                string.IsNullOrEmpty(e.PropertyName))
            {
                InvalidateOverlay();
            }
        }

        private static Vector2 ToPixels(Point point, double scaling)
        {
            return new Vector2((float)(point.X * scaling), (float)(point.Y * scaling));
        }

        private sealed class TransformOverlay : Control
        {
            private readonly InteractiveEffectViewport _owner;

            public TransformOverlay(InteractiveEffectViewport owner)
            {
                _owner = owner;
            }

            public override void Render(DrawingContext context)
            {
                base.Render(context);
                _owner.DrawTransformBox(context);
            }
        }
    }
}
