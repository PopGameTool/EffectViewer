using Avalonia;
using Avalonia.Controls;

namespace EffectViewer.Controls
{
    public partial class DockablePanelView : UserControl
    {
        private const double SplitterWidth = 5d;
        private const double CompactLayoutWidth = 680d;
        private const double CompactSplitterHeight = 5d;
        private const double CompactMinimumPrimaryHeight = 150d;
        private const double CompactMinimumSidePanelHeight = 150d;
        private const double CompactMaximumSidePanelHeight = 320d;
        private double _lastSidePanelWidth = double.NaN;
        private double _lastCompactSidePanelHeight = double.NaN;

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

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateLayoutColumns();
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

        private void CaptureVisibleSidePanelSize(bool compactLayout)
        {
            if (!IsSidePanelVisible)
            {
                return;
            }

            if (compactLayout)
            {
                if (SideContentHost.Bounds.Height > 0)
                {
                    _lastCompactSidePanelHeight = SideContentHost.Bounds.Height;
                }

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
            bool compactLayout = Bounds.Width > 0 && Bounds.Width < CompactLayoutWidth;
            if (captureCurrentWidth)
            {
                CaptureVisibleSidePanelSize(compactLayout);
            }

            if (double.IsNaN(_lastSidePanelWidth) || _lastSidePanelWidth <= 0)
            {
                _lastSidePanelWidth = DefaultSidePanelWidth;
            }

            if (double.IsNaN(_lastCompactSidePanelHeight) || _lastCompactSidePanelHeight <= 0)
            {
                _lastCompactSidePanelHeight = System.Math.Clamp(
                    DefaultSidePanelWidth,
                    CompactMinimumSidePanelHeight,
                    CompactMaximumSidePanelHeight);
            }

            if (compactLayout)
            {
                UpdateCompactLayout();
                return;
            }

            UpdateDockedLayout();
        }

        private void UpdateDockedLayout()
        {
            _lastSidePanelWidth = System.Math.Max(MinimumSidePanelWidth, _lastSidePanelWidth);
            double sideWidth = IsSidePanelVisible ? _lastSidePanelWidth : 0d;
            double sideMinWidth = IsSidePanelVisible ? MinimumSidePanelWidth : 0d;
            double splitterWidth = IsSidePanelVisible ? SplitterWidth : 0d;

            ConfigureRow(LayoutRoot.RowDefinitions[0], 1d, 0d, GridUnitType.Star);
            ConfigureRow(LayoutRoot.RowDefinitions[1], 0d, 0d);
            ConfigureRow(LayoutRoot.RowDefinitions[2], 0d, 0d);
            ConfigureColumn(LayoutRoot.ColumnDefinitions[0], 1d, 260d, GridUnitType.Star);
            ConfigureColumn(LayoutRoot.ColumnDefinitions[1], splitterWidth, splitterWidth);
            ConfigureColumn(LayoutRoot.ColumnDefinitions[2], sideWidth, sideMinWidth);
            SideSplitter.ResizeDirection = GridResizeDirection.Columns;
            SetSplitterOrientationClasses(SideSplitter, horizontal: false);

            Grid.SetRow(PrimaryContentHost, 0);
            Grid.SetRow(SideSplitter, 0);
            Grid.SetRow(SideContentHost, 0);
            Grid.SetRowSpan(PrimaryContentHost, 1);
            Grid.SetRowSpan(SideSplitter, 1);
            Grid.SetRowSpan(SideContentHost, 1);
            Grid.SetColumnSpan(PrimaryContentHost, 1);
            Grid.SetColumnSpan(SideSplitter, 1);
            Grid.SetColumnSpan(SideContentHost, 1);

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

        private void UpdateCompactLayout()
        {
            double availableHeight = Bounds.Height > 0 ? Bounds.Height : 0d;
            double sideHeight = IsSidePanelVisible
                ? System.Math.Max(CompactMinimumSidePanelHeight, _lastCompactSidePanelHeight)
                : 0d;
            if (availableHeight > 0)
            {
                double maximumHeight = System.Math.Max(CompactMinimumSidePanelHeight, availableHeight * 0.45d);
                sideHeight = System.Math.Min(sideHeight, maximumHeight);
            }

            double sideMinHeight = IsSidePanelVisible ? CompactMinimumSidePanelHeight : 0d;
            double splitterHeight = IsSidePanelVisible ? CompactSplitterHeight : 0d;

            ConfigureColumn(LayoutRoot.ColumnDefinitions[0], 1d, 0d, GridUnitType.Star);
            ConfigureColumn(LayoutRoot.ColumnDefinitions[1], 0d, 0d);
            ConfigureColumn(LayoutRoot.ColumnDefinitions[2], 0d, 0d);
            ConfigureRow(LayoutRoot.RowDefinitions[0], 1d, CompactMinimumPrimaryHeight, GridUnitType.Star);
            ConfigureRow(LayoutRoot.RowDefinitions[1], splitterHeight, splitterHeight);
            ConfigureRow(LayoutRoot.RowDefinitions[2], sideHeight, sideMinHeight);

            Grid.SetColumn(PrimaryContentHost, 0);
            Grid.SetColumn(SideSplitter, 0);
            Grid.SetColumn(SideContentHost, 0);
            Grid.SetColumnSpan(PrimaryContentHost, 3);
            Grid.SetColumnSpan(SideSplitter, 3);
            Grid.SetColumnSpan(SideContentHost, 3);
            Grid.SetRow(PrimaryContentHost, 0);
            Grid.SetRow(SideSplitter, 1);
            Grid.SetRow(SideContentHost, 2);
            Grid.SetRowSpan(PrimaryContentHost, 1);
            Grid.SetRowSpan(SideSplitter, 1);
            Grid.SetRowSpan(SideContentHost, 1);

            SideSplitter.ResizeDirection = GridResizeDirection.Rows;
            SetSplitterOrientationClasses(SideSplitter, horizontal: true);
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

        private static void ConfigureRow(
            RowDefinition row,
            double height,
            double minHeight,
            GridUnitType unitType = GridUnitType.Pixel)
        {
            row.MinHeight = minHeight;
            row.Height = new GridLength(height, unitType);
        }

        private static void SetSplitterOrientationClasses(GridSplitter splitter, bool horizontal)
        {
            SetClass(splitter, "horizontal-splitter", horizontal);
            SetClass(splitter, "vertical-splitter", !horizontal);
        }

        private static void SetClass(Control control, string className, bool isSet)
        {
            if (isSet)
            {
                if (!control.Classes.Contains(className))
                {
                    control.Classes.Add(className);
                }

                return;
            }

            control.Classes.Remove(className);
        }
    }
}
