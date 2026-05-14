using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EffectViewer.EffectRuntime.Common;
using EffectViewer.ViewModels;

namespace EffectViewer.Controls
{
    public sealed class FloatParameterTrackPreview : Control
    {
        public static readonly StyledProperty<FloatParameterTrackViewModel> TrackProperty =
            AvaloniaProperty.Register<FloatParameterTrackPreview, FloatParameterTrackViewModel>(nameof(Track));

        private readonly Pen _gridPen = new(Brushes.Gray, 1);
        private readonly Pen _curvePen = new(Brushes.DeepSkyBlue, 2);

        public FloatParameterTrackViewModel Track
        {
            get => GetValue(TrackProperty);
            set => SetValue(TrackProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TrackProperty)
            {
                if (change.OldValue is FloatParameterTrackViewModel oldTrack)
                {
                    oldTrack.Changed -= OnTrackChanged;
                    oldTrack.Nodes.CollectionChanged -= OnTrackNodesChanged;
                }

                if (change.NewValue is FloatParameterTrackViewModel newTrack)
                {
                    newTrack.Changed += OnTrackChanged;
                    newTrack.Nodes.CollectionChanged += OnTrackNodesChanged;
                }

                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            Rect bounds = new(Bounds.Size);
            if (bounds.Width <= 2 || bounds.Height <= 2)
            {
                return;
            }

            context.FillRectangle(Brushes.Transparent, bounds);
            DrawGrid(context, bounds);

            if (Track is null || Track.Nodes.Count == 0)
            {
                return;
            }

            FloatParameterTrack track = new();
            Track.ApplyTo(track);
            if (track.mCountNodes <= 0)
            {
                return;
            }

            double min = Track.Nodes.Min(node => Math.Min(node.LowValue, node.HighValue));
            double max = Track.Nodes.Max(node => Math.Max(node.LowValue, node.HighValue));
            if (Math.Abs(max - min) < 0.0001)
            {
                min -= 1;
                max += 1;
            }

            StreamGeometry geometry = new();
            using (StreamGeometryContext stream = geometry.Open())
            {
                const int samples = 72;
                for (int i = 0; i < samples; i++)
                {
                    double t = i / (double)(samples - 1);
                    float value = Definition.FloatTrackEvaluate(track, (float)t, 0.5f);
                    double x = bounds.X + t * bounds.Width;
                    double y = bounds.Bottom - ((value - min) / (max - min)) * bounds.Height;
                    Point point = new(x, Math.Clamp(y, bounds.Top, bounds.Bottom));
                    if (i == 0)
                    {
                        stream.BeginFigure(point, false);
                    }
                    else
                    {
                        stream.LineTo(point);
                    }
                }
            }

            context.DrawGeometry(null, _curvePen, geometry);
        }

        protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            if (Track is not null)
            {
                Track.Changed -= OnTrackChanged;
                Track.Nodes.CollectionChanged -= OnTrackNodesChanged;
            }

            base.OnDetachedFromVisualTree(e);
        }

        private void DrawGrid(DrawingContext context, Rect bounds)
        {
            for (int i = 1; i < 4; i++)
            {
                double x = bounds.X + bounds.Width * i / 4d;
                context.DrawLine(_gridPen, new Point(x, bounds.Top), new Point(x, bounds.Bottom));
            }

            for (int i = 1; i < 3; i++)
            {
                double y = bounds.Y + bounds.Height * i / 3d;
                context.DrawLine(_gridPen, new Point(bounds.Left, y), new Point(bounds.Right, y));
            }
        }

        private void OnTrackChanged(FloatParameterTrackViewModel track)
        {
            InvalidateVisual();
        }

        private void OnTrackNodesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateVisual();
        }
    }
}
