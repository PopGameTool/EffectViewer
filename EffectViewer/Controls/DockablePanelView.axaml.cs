using Avalonia;
using Avalonia.Controls;

namespace EffectViewer.Controls
{
    public partial class DockablePanelView : UserControl
    {
        private const double SplitterWidth = 5d;
        private double _lastSidePanelWidth = double.NaN;

        public static readonly StyledProperty<object> PrimaryContentProperty =
            AvaloniaProperty.Register<DockablePanelView, object>(nameof(PrimaryContent));

        public static readonly StyledProperty<object> SideContentProperty =
            AvaloniaProperty.Register<DockablePanelView, object>(nameof(SideContent));

        public static readonly StyledProperty<bool> IsSidePanelVisibleProperty =
            AvaloniaProperty.Register<DockablePanelView, bool>(nameof(IsSidePanelVisible), true);

        public static readonly StyledProperty<bool> IsSidePanelOnLeftProperty =
            AvaloniaProperty.Register<DockablePanelView, bool>(nameof(IsSidePanelOnLeft));

        public static readonly StyledProperty<double> DefaultSidePanelWidthProperty =
            AvaloniaProperty.Register<DockablePanelView, double>(nameof(DefaultSidePanelWidth), 320d);

        public static readonly StyledProperty<double> MinimumSidePanelWidthProperty =
            AvaloniaProperty.Register<DockablePanelView, double>(nameof(MinimumSidePanelWidth), 180d);

        public static readonly StyledProperty<int> LayoutResetRevisionProperty =
            AvaloniaProperty.Register<DockablePanelView, int>(nameof(LayoutResetRevision));

        public object PrimaryContent
        {
            get => GetValue(PrimaryContentProperty);
            set => SetValue(PrimaryContentProperty, value);
        }

        public object SideContent
        {
            get => GetValue(SideContentProperty);
            set => SetValue(SideContentProperty, value);
        }

        public bool IsSidePanelVisible
        {
            get => GetValue(IsSidePanelVisibleProperty);
            set => SetValue(IsSidePanelVisibleProperty, value);
        }

        public bool IsSidePanelOnLeft
        {
            get => GetValue(IsSidePanelOnLeftProperty);
            set => SetValue(IsSidePanelOnLeftProperty, value);
        }

        public double DefaultSidePanelWidth
        {
            get => GetValue(DefaultSidePanelWidthProperty);
            set => SetValue(DefaultSidePanelWidthProperty, value);
        }

        public double MinimumSidePanelWidth
        {
            get => GetValue(MinimumSidePanelWidthProperty);
            set => SetValue(MinimumSidePanelWidthProperty, value);
        }

        public int LayoutResetRevision
        {
            get => GetValue(LayoutResetRevisionProperty);
            set => SetValue(LayoutResetRevisionProperty, value);
        }

        public DockablePanelView()
        {
            InitializeComponent();
            PrimaryContentHost.Content = PrimaryContent;
            SideContentHost.Content = SideContent;
            UpdateLayoutColumns(captureCurrentWidth: false);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == PrimaryContentProperty)
            {
                PrimaryContentHost.Content = change.NewValue;
            }
            else if (change.Property == SideContentProperty)
            {
                SideContentHost.Content = change.NewValue;
            }
            else if (change.Property == IsSidePanelVisibleProperty
                     || change.Property == IsSidePanelOnLeftProperty
                     || change.Property == MinimumSidePanelWidthProperty)
            {
                UpdateLayoutColumns();
            }
            else if (change.Property == DefaultSidePanelWidthProperty)
            {
                if (double.IsNaN(_lastSidePanelWidth) || _lastSidePanelWidth <= 0)
                {
                    _lastSidePanelWidth = DefaultSidePanelWidth;
                }

                UpdateLayoutColumns();
            }
            else if (change.Property == LayoutResetRevisionProperty)
            {
                _lastSidePanelWidth = DefaultSidePanelWidth;
                UpdateLayoutColumns(captureCurrentWidth: false);
            }
        }

        private void CaptureVisibleSidePanelWidth()
        {
            if (!IsSidePanelVisible)
            {
                return;
            }

            ColumnDefinition sideColumn = LayoutRoot.ColumnDefinitions[IsSidePanelOnLeft ? 0 : 2];
            if (sideColumn.ActualWidth > 0)
            {
                _lastSidePanelWidth = sideColumn.ActualWidth;
            }
        }

        private void UpdateLayoutColumns(bool captureCurrentWidth = true)
        {
            if (captureCurrentWidth)
            {
                CaptureVisibleSidePanelWidth();
            }

            if (double.IsNaN(_lastSidePanelWidth) || _lastSidePanelWidth <= 0)
            {
                _lastSidePanelWidth = DefaultSidePanelWidth;
            }

            _lastSidePanelWidth = System.Math.Max(MinimumSidePanelWidth, _lastSidePanelWidth);
            double sideWidth = IsSidePanelVisible ? _lastSidePanelWidth : 0d;
            double sideMinWidth = IsSidePanelVisible ? MinimumSidePanelWidth : 0d;
            double splitterWidth = IsSidePanelVisible ? SplitterWidth : 0d;

            if (IsSidePanelOnLeft)
            {
                ConfigureColumn(LayoutRoot.ColumnDefinitions[0], sideWidth, sideMinWidth);
                ConfigureColumn(LayoutRoot.ColumnDefinitions[1], splitterWidth, splitterWidth);
                ConfigureColumn(LayoutRoot.ColumnDefinitions[2], 1d, 260d, GridUnitType.Star);
                Grid.SetColumn(SideContentHost, 0);
                Grid.SetColumn(SideSplitter, 1);
                Grid.SetColumn(PrimaryContentHost, 2);
            }
            else
            {
                ConfigureColumn(LayoutRoot.ColumnDefinitions[0], 1d, 260d, GridUnitType.Star);
                ConfigureColumn(LayoutRoot.ColumnDefinitions[1], splitterWidth, splitterWidth);
                ConfigureColumn(LayoutRoot.ColumnDefinitions[2], sideWidth, sideMinWidth);
                Grid.SetColumn(PrimaryContentHost, 0);
                Grid.SetColumn(SideSplitter, 1);
                Grid.SetColumn(SideContentHost, 2);
            }

            SideSplitter.IsVisible = IsSidePanelVisible;
            SideContentHost.IsVisible = IsSidePanelVisible;
        }

        private static void ConfigureColumn(
            ColumnDefinition column,
            double width,
            double minWidth,
            GridUnitType unitType = GridUnitType.Pixel)
        {
            column.MinWidth = minWidth;
            column.Width = new GridLength(width, unitType);
        }
    }
}
