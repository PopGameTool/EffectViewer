using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using EffectViewer.ViewModels;

namespace EffectViewer.Controls
{
    public sealed class ReanimTimelineControl : Control
    {
        private const double TrackColumnWidth = 150d;
        private const double HeaderHeight = 24d;
        private const double RowHeight = 28d;
        private const double FrameWidth = 28d;
        private const double CellWidth = 22d;
        private const double CellHeight = 18d;
        private const double VisibilityBoxSize = 14d;

        private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.FromRgb(24, 24, 24));
        private static readonly IBrush HeaderBrush = new SolidColorBrush(Color.FromRgb(34, 34, 34));
        private static readonly IBrush AlternateRowBrush = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));
        private static readonly IBrush SelectedTrackBrush = new SolidColorBrush(Color.FromArgb(50, 78, 166, 255));
        private static readonly IBrush CellEmptyBrush = new SolidColorBrush(Color.FromRgb(42, 42, 42));
        private static readonly IBrush CellContentBrush = new SolidColorBrush(Color.FromArgb(48, 78, 166, 255));
        private static readonly IBrush CellTweenBrush = new SolidColorBrush(Color.FromArgb(56, 255, 198, 77));
        private static readonly IBrush CellSelectedBrush = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255));
        private static readonly IBrush CellPlayheadBrush = new SolidColorBrush(Color.FromArgb(90, 255, 90, 90));
        private static readonly IBrush TextBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230));
        private static readonly IBrush MutedTextBrush = new SolidColorBrush(Color.FromRgb(170, 170, 170));
        private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 1);
        private static readonly Pen CellPen = new(new SolidColorBrush(Color.FromArgb(85, 255, 255, 255)), 1);
        private static readonly Pen ContentPen = new(new SolidColorBrush(Color.FromRgb(123, 190, 255)), 1);
        private static readonly Pen TweenPen = new(new SolidColorBrush(Color.FromRgb(245, 194, 74)), 1);
        private static readonly Pen SelectedPen = new(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 2);
        private static readonly FontFamily TextFontFamily = new("avares://EffectViewer/Assets/Fonts#MiSans");

        private ScrollViewer _scrollViewer;
        private EffectEditorViewModel _subscribedEditor;
        private readonly HashSet<ReanimTrackViewModel> _subscribedTracks = [];

        public static readonly StyledProperty<EffectEditorViewModel> EditorProperty =
            AvaloniaProperty.Register<ReanimTimelineControl, EffectEditorViewModel>(nameof(Editor));

        public EffectEditorViewModel Editor
        {
            get => GetValue(EditorProperty);
            set => SetValue(EditorProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int frameCount = Math.Max(1, Editor?.ReanimFrameCount ?? 0);
            int trackCount = Math.Max(1, Editor?.ReanimTracks.Count ?? 0);
            return new Size(
                TrackColumnWidth + frameCount * FrameWidth,
                HeaderHeight + trackCount * RowHeight);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _scrollViewer = this.FindAncestorOfType<ScrollViewer>();
            if (_scrollViewer is not null)
            {
                _scrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
            }

            AttachEditor(Editor);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_scrollViewer is not null)
            {
                _scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
                _scrollViewer = null;
            }

            DetachEditor(_subscribedEditor);
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == EditorProperty)
            {
                if (change.OldValue is EffectEditorViewModel oldEditor)
                {
                    DetachEditor(oldEditor);
                }

                if (change.NewValue is EffectEditorViewModel newEditor)
                {
                    AttachEditor(newEditor);
                }

                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return;
            }

            Rect visible = GetVisibleContentRect();
            context.FillRectangle(BackgroundBrush, visible);

            EffectEditorViewModel editor = Editor;
            if (editor is null || editor.ReanimTracks.Count == 0 || editor.ReanimFrameCount == 0)
            {
                DrawText(context, "No frames", new Point(visible.Left + 8, visible.Top + 6), MutedTextBrush, 12);
                return;
            }

            DrawRows(context, editor, visible);
            DrawGridLines(context, editor, visible);
            DrawTrackHeaders(context, editor, visible);
            DrawHeader(context, editor, visible);
            DrawCorner(context, visible);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (Editor is null)
            {
                return;
            }

            PointerPoint point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            Point position = e.GetPosition(this);
            Rect visible = GetVisibleContentRect();
            int frameIndex = (int)Math.Floor((position.X - TrackColumnWidth) / FrameWidth);
            int trackIndex = (int)Math.Floor((position.Y - HeaderHeight) / RowHeight);

            if (position.Y < visible.Top + HeaderHeight)
            {
                if (position.X >= visible.Left + TrackColumnWidth && frameIndex >= 0)
                {
                    Editor.SelectReanimTimelineFrame(frameIndex);
                    e.Handled = true;
                }

                return;
            }

            if (trackIndex < 0 || trackIndex >= Editor.ReanimTracks.Count)
            {
                return;
            }

            if (position.X < visible.Left + TrackColumnWidth)
            {
                double rowTop = HeaderHeight + trackIndex * RowHeight;
                Rect visibilityRect = new(visible.Left + 8, rowTop + 7, VisibilityBoxSize, VisibilityBoxSize);
                if (visibilityRect.Contains(position))
                {
                    Editor.ToggleReanimTimelineTrackVisibility(trackIndex);
                }
                else
                {
                    Editor.SelectReanimTimelineCell(trackIndex, Editor.SelectedReanimFrameIndex);
                }

                e.Handled = true;
                return;
            }

            if (frameIndex >= 0)
            {
                Editor.SelectReanimTimelineCell(trackIndex, frameIndex);
                e.Handled = true;
            }
        }

        private void DrawHeader(DrawingContext context, EffectEditorViewModel editor, Rect visible)
        {
            Rect headerBounds = new(visible.Left + TrackColumnWidth, visible.Top, Math.Max(0, visible.Width - TrackColumnWidth), HeaderHeight);
            if (headerBounds.Width <= 0)
            {
                return;
            }

            context.FillRectangle(HeaderBrush, headerBounds);
            context.DrawLine(GridPen, new Point(headerBounds.Left, headerBounds.Bottom - 0.5), new Point(headerBounds.Right, headerBounds.Bottom - 0.5));

            using (context.PushClip(headerBounds))
            {
                int firstFrame = FirstVisibleFrame(visible);
                int lastFrame = LastVisibleFrame(visible, editor.ReanimFrameCount);
                for (int frameIndex = firstFrame; frameIndex <= lastFrame; frameIndex++)
                {
                    double x = TrackColumnWidth + frameIndex * FrameWidth;
                    Rect frameRect = new(x, visible.Top, FrameWidth, HeaderHeight);
                    if (frameIndex == editor.SelectedReanimFrameIndex)
                    {
                        context.FillRectangle(CellPlayheadBrush, frameRect);
                    }

                    context.DrawLine(GridPen, new Point(frameRect.Left, frameRect.Top), new Point(frameRect.Left, frameRect.Bottom));
                    DrawText(
                        context,
                        (frameIndex + 1).ToString(CultureInfo.InvariantCulture),
                        new Point(x + 4, visible.Top + 4),
                        TextBrush,
                        10);
                }
            }
        }

        private void DrawRows(DrawingContext context, EffectEditorViewModel editor, Rect visible)
        {
            int firstTrack = FirstVisibleTrack(visible);
            int lastTrack = LastVisibleTrack(visible, editor.ReanimTracks.Count);
            int firstFrame = FirstVisibleFrame(visible);
            int lastFrame = LastVisibleFrame(visible, editor.ReanimFrameCount);

            for (int trackIndex = firstTrack; trackIndex <= lastTrack; trackIndex++)
            {
                double rowTop = HeaderHeight + trackIndex * RowHeight;
                Rect rowRect = new(visible.X, rowTop, visible.Width, RowHeight);
                if (trackIndex % 2 == 1)
                {
                    context.FillRectangle(AlternateRowBrush, rowRect);
                }

                if (ReferenceEquals(editor.ReanimTracks[trackIndex], editor.SelectedReanimTrack))
                {
                    context.FillRectangle(SelectedTrackBrush, rowRect);
                }

                DrawTrackFrames(context, editor, trackIndex, firstFrame, lastFrame, rowTop);
            }
        }

        private void DrawTrackHeaders(DrawingContext context, EffectEditorViewModel editor, Rect visible)
        {
            Rect headerBounds = new(visible.Left, visible.Top + HeaderHeight, TrackColumnWidth, Math.Max(0, visible.Height - HeaderHeight));
            if (headerBounds.Height <= 0)
            {
                return;
            }

            using (context.PushClip(headerBounds))
            {
                int firstTrack = FirstVisibleTrack(visible);
                int lastTrack = LastVisibleTrack(visible, editor.ReanimTracks.Count);
                for (int trackIndex = firstTrack; trackIndex <= lastTrack; trackIndex++)
                {
                    double rowTop = HeaderHeight + trackIndex * RowHeight;
                    ReanimTrackViewModel track = editor.ReanimTracks[trackIndex];
                    Rect rowRect = new(visible.Left, rowTop, TrackColumnWidth, RowHeight);
                    context.FillRectangle(trackIndex % 2 == 1 ? AlternateRowBrush : BackgroundBrush, rowRect);
                    if (ReferenceEquals(track, editor.SelectedReanimTrack))
                    {
                        context.FillRectangle(SelectedTrackBrush, rowRect);
                    }

                    DrawTrackHeader(context, track, visible.Left, rowTop);
                }
            }
        }

        private void DrawTrackHeader(DrawingContext context, ReanimTrackViewModel track, double left, double rowTop)
        {
            Rect nameRect = new(left, rowTop, TrackColumnWidth, RowHeight);
            Rect visibilityRect = new(left + 8, rowTop + 7, VisibilityBoxSize, VisibilityBoxSize);
            context.DrawRectangle(null, CellPen, visibilityRect, 2, 2);
            if (track.IsVisible)
            {
                context.DrawLine(ContentPen, visibilityRect.TopLeft + new Vector(3, 7), visibilityRect.TopLeft + new Vector(6, 10));
                context.DrawLine(ContentPen, visibilityRect.TopLeft + new Vector(6, 10), visibilityRect.TopLeft + new Vector(11, 4));
            }

            using (context.PushClip(nameRect))
            {
                DrawText(context, track.DisplayName, new Point(left + 30, rowTop + 6), track.IsVisible ? TextBrush : MutedTextBrush, 12);
            }
        }

        private void DrawTrackFrames(
            DrawingContext context,
            EffectEditorViewModel editor,
            int trackIndex,
            int firstFrame,
            int lastFrame,
            double rowTop)
        {
            Rect gridBounds = GetGridBodyRect(GetVisibleContentRect());
            if (gridBounds.Width <= 0 || gridBounds.Height <= 0)
            {
                return;
            }

            using (context.PushClip(gridBounds))
            {
                for (int frameIndex = firstFrame; frameIndex <= lastFrame; frameIndex++)
                {
                    double frameLeft = TrackColumnWidth + frameIndex * FrameWidth;
                    double cellLeft = frameLeft + (FrameWidth - CellWidth) / 2d;
                    double cellTop = rowTop + (RowHeight - CellHeight) / 2d;
                    Rect cellRect = new(cellLeft, cellTop, CellWidth, CellHeight);

                    editor.GetReanimTimelineFrameState(
                        trackIndex,
                        frameIndex,
                        out bool hasContent,
                        out bool hasImage,
                        out bool isTweened);

                    IBrush fill = isTweened ? CellTweenBrush : hasContent ? CellContentBrush : CellEmptyBrush;
                    Pen pen = isTweened ? TweenPen : hasContent ? ContentPen : CellPen;
                    context.DrawRectangle(fill, pen, cellRect, 2, 2);

                    bool isPlayhead = frameIndex == editor.SelectedReanimFrameIndex;
                    bool isSelected = isPlayhead && ReferenceEquals(editor.ReanimTracks[trackIndex], editor.SelectedReanimTrack);
                    if (isPlayhead)
                    {
                        context.DrawRectangle(CellPlayheadBrush, null, cellRect, 2, 2);
                    }

                    if (isSelected)
                    {
                        context.DrawRectangle(CellSelectedBrush, SelectedPen, cellRect, 2, 2);
                    }

                    string glyph = isTweened ? "T" : hasContent ? hasImage ? "I" : "*" : string.Empty;
                    if (!string.IsNullOrEmpty(glyph))
                    {
                        DrawText(context, glyph, new Point(cellLeft + 7, cellTop + 1), TextBrush, 10, FontWeight.SemiBold);
                    }
                }
            }
        }

        private void DrawGridLines(DrawingContext context, EffectEditorViewModel editor, Rect visible)
        {
            Rect gridBounds = GetGridBodyRect(visible);
            if (gridBounds.Width <= 0 || gridBounds.Height <= 0)
            {
                return;
            }

            using (context.PushClip(gridBounds))
            {
                int firstFrame = FirstVisibleFrame(visible);
                int lastFrame = LastVisibleFrame(visible, editor.ReanimFrameCount);
                for (int frameIndex = firstFrame; frameIndex <= lastFrame + 1; frameIndex++)
                {
                    double x = TrackColumnWidth + frameIndex * FrameWidth;
                    context.DrawLine(GridPen, new Point(x, gridBounds.Top), new Point(x, gridBounds.Bottom));
                }

                int firstTrack = FirstVisibleTrack(visible);
                int lastTrack = LastVisibleTrack(visible, editor.ReanimTracks.Count);
                for (int trackIndex = firstTrack; trackIndex <= lastTrack + 1; trackIndex++)
                {
                    double y = HeaderHeight + trackIndex * RowHeight;
                    context.DrawLine(GridPen, new Point(gridBounds.Left, y), new Point(gridBounds.Right, y));
                }
            }
        }

        private void DrawCorner(DrawingContext context, Rect visible)
        {
            Rect corner = new(visible.Left, visible.Top, TrackColumnWidth, HeaderHeight);
            context.FillRectangle(HeaderBrush, corner);
            context.DrawLine(GridPen, new Point(corner.Left, corner.Bottom - 0.5), new Point(corner.Right, corner.Bottom - 0.5));
            context.DrawLine(GridPen, new Point(corner.Right - 0.5, corner.Top), new Point(corner.Right - 0.5, visible.Bottom));
        }

        private Rect GetVisibleContentRect()
        {
            Rect bounds = new(Bounds.Size);
            if (_scrollViewer is null)
            {
                return bounds;
            }

            Vector offset = _scrollViewer.Offset;
            Size viewport = _scrollViewer.Viewport;
            if (viewport.Width <= 0 || viewport.Height <= 0)
            {
                viewport = _scrollViewer.Bounds.Size;
            }

            Rect visible = new(offset.X, offset.Y, viewport.Width, viewport.Height);
            return bounds.Intersect(visible);
        }

        private static int FirstVisibleFrame(Rect visible)
        {
            return Math.Max(0, (int)Math.Floor((visible.Left - TrackColumnWidth) / FrameWidth));
        }

        private static int LastVisibleFrame(Rect visible, int frameCount)
        {
            return Math.Clamp((int)Math.Ceiling((visible.Right - TrackColumnWidth) / FrameWidth), 0, Math.Max(0, frameCount - 1));
        }

        private static int FirstVisibleTrack(Rect visible)
        {
            return Math.Max(0, (int)Math.Floor((visible.Top - HeaderHeight) / RowHeight));
        }

        private static int LastVisibleTrack(Rect visible, int trackCount)
        {
            return Math.Clamp((int)Math.Ceiling((visible.Bottom - HeaderHeight) / RowHeight), 0, Math.Max(0, trackCount - 1));
        }

        private static Rect GetGridBodyRect(Rect visible)
        {
            return new(
                visible.Left + TrackColumnWidth,
                visible.Top + HeaderHeight,
                Math.Max(0, visible.Width - TrackColumnWidth),
                Math.Max(0, visible.Height - HeaderHeight));
        }

        private static void DrawText(
            DrawingContext context,
            string text,
            Point origin,
            IBrush brush,
            double fontSize,
            FontWeight fontWeight = default)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            FormattedText formattedText = new(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(TextFontFamily, FontStyle.Normal, fontWeight == default ? FontWeight.Normal : fontWeight),
                fontSize,
                brush);
            context.DrawText(formattedText, origin);
        }

        private void AttachEditor(EffectEditorViewModel editor)
        {
            if (editor is null || ReferenceEquals(_subscribedEditor, editor))
            {
                return;
            }

            DetachEditor(_subscribedEditor);
            _subscribedEditor = editor;
            editor.PropertyChanged += OnEditorPropertyChanged;
            editor.ReanimTracks.CollectionChanged += OnTracksCollectionChanged;
            foreach (ReanimTrackViewModel track in editor.ReanimTracks)
            {
                AttachTrack(track);
            }
        }

        private void DetachEditor(EffectEditorViewModel editor)
        {
            if (editor is null)
            {
                return;
            }

            editor.PropertyChanged -= OnEditorPropertyChanged;
            editor.ReanimTracks.CollectionChanged -= OnTracksCollectionChanged;
            DetachAllTracks();

            if (ReferenceEquals(_subscribedEditor, editor))
            {
                _subscribedEditor = null;
            }
        }

        private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EffectEditorViewModel.ReanimTimelineRevision) ||
                e.PropertyName == nameof(EffectEditorViewModel.SelectedReanimFrameIndex) ||
                e.PropertyName == nameof(EffectEditorViewModel.SelectedReanimTrack))
            {
                InvalidateVisual();
            }
            else if (e.PropertyName == nameof(EffectEditorViewModel.ReanimFrameCount) ||
                     e.PropertyName == nameof(EffectEditorViewModel.ReanimTrackCount))
            {
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        private void OnTracksCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems is not null)
            {
                foreach (ReanimTrackViewModel track in e.OldItems)
                {
                    DetachTrack(track);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                DetachAllTracks();
            }

            if (e.NewItems is not null)
            {
                foreach (ReanimTrackViewModel track in e.NewItems)
                {
                    AttachTrack(track);
                }
            }
            else if (_subscribedEditor is not null)
            {
                foreach (ReanimTrackViewModel track in _subscribedEditor.ReanimTracks)
                {
                    AttachTrack(track);
                }
            }

            InvalidateMeasure();
            InvalidateVisual();
        }

        private void AttachTrack(ReanimTrackViewModel track)
        {
            if (track is not null && _subscribedTracks.Add(track))
            {
                track.PropertyChanged += OnTrackPropertyChanged;
            }
        }

        private void DetachTrack(ReanimTrackViewModel track)
        {
            if (track is not null && _subscribedTracks.Remove(track))
            {
                track.PropertyChanged -= OnTrackPropertyChanged;
            }
        }

        private void DetachAllTracks()
        {
            foreach (ReanimTrackViewModel track in _subscribedTracks)
            {
                track.PropertyChanged -= OnTrackPropertyChanged;
            }

            _subscribedTracks.Clear();
        }

        private void OnTrackPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            InvalidateVisual();
        }

        private void OnScrollViewerPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            InvalidateVisual();
        }
    }
}
