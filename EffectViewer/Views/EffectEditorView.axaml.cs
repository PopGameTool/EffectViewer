using Avalonia;
using Avalonia.Controls;
using EffectViewer.ViewModels;

namespace EffectViewer.Views
{
    public partial class EffectEditorView : UserControl
    {
        private const double TimelineSplitterHeight = 5d;
        private const double TimelineDefaultHeight = 230d;
        private double _lastTimelineHeight = TimelineDefaultHeight;

        public EffectEditorView()
        {
            InitializeComponent();
            UpdateTimelineRows();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DataContextProperty)
            {
                UpdateTimelineRows();
            }
        }

        protected override void OnDataContextChanged(System.EventArgs e)
        {
            base.OnDataContextChanged(e);
            UpdateTimelineRows();
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            CaptureTimelineHeight();
        }

        private void CaptureTimelineHeight()
        {
            if (DataContext is not EffectEditorViewModel { HasReanimControls: true } ||
                EditorContentGrid.RowDefinitions.Count < 3)
            {
                return;
            }

            double actualHeight = EditorContentGrid.RowDefinitions[2].ActualHeight;
            if (actualHeight > 0)
            {
                _lastTimelineHeight = actualHeight;
            }
        }

        private void UpdateTimelineRows()
        {
            if (EditorContentGrid is null || EditorContentGrid.RowDefinitions.Count < 3)
            {
                return;
            }

            bool showTimeline = DataContext is EffectEditorViewModel { HasReanimControls: true };
            if (showTimeline)
            {
                EditorContentGrid.RowDefinitions[1].MinHeight = TimelineSplitterHeight;
                EditorContentGrid.RowDefinitions[1].Height = new GridLength(TimelineSplitterHeight);
                EditorContentGrid.RowDefinitions[2].MinHeight = 120d;
                EditorContentGrid.RowDefinitions[2].Height = new GridLength(
                    System.Math.Max(120d, _lastTimelineHeight));
            }
            else
            {
                CaptureTimelineHeight();
                EditorContentGrid.RowDefinitions[1].MinHeight = 0d;
                EditorContentGrid.RowDefinitions[1].Height = new GridLength(0d);
                EditorContentGrid.RowDefinitions[2].MinHeight = 0d;
                EditorContentGrid.RowDefinitions[2].Height = new GridLength(0d);
            }
        }
    }
}
