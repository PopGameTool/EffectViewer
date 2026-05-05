using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace EffectViewer.Controls
{
    public partial class DockablePanelView : UserControl
    {
        private const double SplitterWidth = 5d;
        private const double CompactLayoutWidth = 680d;
        private const double CompactMinimumPrimaryHeight = 280d;
        private const double CompactDefaultPrimaryHeight = 360d;
        private const double CompactMinimumSidePanelHeight = 420d;
        private const double CompactDefaultSidePanelHeight = 520d;
        private double _lastSidePanelWidth = double.NaN;
        private double _lastCompactSidePanelHeight = double.NaN;
        private bool _isCompactLayoutActive;

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

        public static readonly StyledProperty<double> SidePanelWidthProperty =
            AvaloniaProperty.Register<DockablePanelView, double>(
                nameof(SidePanelWidth),
                defaultValue: 0d,
                defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<double> CompactSidePanelHeightProperty =
            AvaloniaProperty.Register<DockablePanelView, double>(
                nameof(CompactSidePanelHeight),
                defaultValue: 0d,
                defaultBindingMode: BindingMode.TwoWay);

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

        public double SidePanelWidth
        {
            get => GetValue(SidePanelWidthProperty);
            set => SetValue(SidePanelWidthProperty, value);
        }

        public double CompactSidePanelHeight
        {
            get => GetValue(CompactSidePanelHeightProperty);
            set => SetValue(CompactSidePanelHeightProperty, value);
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
                    PublishSidePanelWidth();
                }

                UpdateLayoutColumns();
            }
            else if (change.Property == SidePanelWidthProperty)
            {
                if (TryGetPositiveDouble(change.NewValue, out double sidePanelWidth))
                {
                    _lastSidePanelWidth = sidePanelWidth;
                    UpdateLayoutColumns(captureCurrentWidth: false);
                }
            }
            else if (change.Property == CompactSidePanelHeightProperty)
            {
                if (TryGetPositiveDouble(change.NewValue, out double compactSidePanelHeight))
                {
                    _lastCompactSidePanelHeight = compactSidePanelHeight;
                    UpdateLayoutColumns(captureCurrentWidth: false);
                }
            }
            else if (change.Property == LayoutResetRevisionProperty)
            {
                _lastSidePanelWidth = DefaultSidePanelWidth;
                _lastCompactSidePanelHeight = CompactDefaultSidePanelHeight;
                PublishSidePanelWidth();
                PublishCompactSidePanelHeight();
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
                return;
            }

            ColumnDefinition sideColumn = LayoutRoot.ColumnDefinitions[IsSidePanelOnLeft ? 0 : 2];
            if (sideColumn.ActualWidth > 0)
            {
                _lastSidePanelWidth = sideColumn.ActualWidth;
                PublishSidePanelWidth();
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
                _lastCompactSidePanelHeight = CompactDefaultSidePanelHeight;
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
            if (_isCompactLayoutActive ||
                PrimaryContentHost.Parent != LayoutRoot ||
                SideContentHost.Parent != LayoutRoot ||
                SideSplitter.Parent != LayoutRoot)
            {
                AttachDockedLayout();
            }

            _isCompactLayoutActive = false;
            SetClass(this, "compact-layout", false);
            LayoutRoot.IsVisible = true;
            CompactScrollViewer.IsVisible = false;
            ResetCompactChildSizing();

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
            bool sideContentIsAttached = SideContentHost.Parent == CompactLayoutRoot;
            if (!_isCompactLayoutActive ||
                PrimaryContentHost.Parent != CompactLayoutRoot ||
                sideContentIsAttached != IsSidePanelVisible)
            {
                AttachCompactLayout();
            }

            _isCompactLayoutActive = true;
            SetClass(this, "compact-layout", true);
            LayoutRoot.IsVisible = false;
            CompactScrollViewer.IsVisible = true;
            UpdateCompactChildSizing();

            ConfigureColumn(LayoutRoot.ColumnDefinitions[0], 1d, 0d, GridUnitType.Star);
            ConfigureColumn(LayoutRoot.ColumnDefinitions[1], 0d, 0d);
            ConfigureColumn(LayoutRoot.ColumnDefinitions[2], 0d, 0d);
            ConfigureRow(LayoutRoot.RowDefinitions[0], 0d, 0d);
            ConfigureRow(LayoutRoot.RowDefinitions[1], 0d, 0d);
            ConfigureRow(LayoutRoot.RowDefinitions[2], 0d, 0d);

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
            SideSplitter.IsVisible = false;
            SideContentHost.IsVisible = IsSidePanelVisible;
        }

        private void AttachDockedLayout()
        {
            LayoutRoot.Children.Clear();
            RemoveFromParent(PrimaryContentHost);
            RemoveFromParent(SideContentHost);
            RemoveFromParent(SideSplitter);
            LayoutRoot.Children.Add(PrimaryContentHost);
            LayoutRoot.Children.Add(SideContentHost);
            LayoutRoot.Children.Add(SideSplitter);
        }

        private void AttachCompactLayout()
        {
            RemoveFromParent(PrimaryContentHost);
            RemoveFromParent(SideContentHost);
            CompactLayoutRoot.Children.Clear();
            CompactLayoutRoot.Children.Add(PrimaryContentHost);
            if (IsSidePanelVisible)
            {
                CompactLayoutRoot.Children.Add(SideContentHost);
            }
        }

        private void UpdateCompactChildSizing()
        {
            double availableHeight = Bounds.Height > 0 ? Bounds.Height : 0d;
            double primaryHeight = availableHeight > 0
                ? System.Math.Clamp(availableHeight * 0.52d, CompactMinimumPrimaryHeight, CompactDefaultPrimaryHeight)
                : CompactDefaultPrimaryHeight;

            PrimaryContentHost.Height = primaryHeight;
            PrimaryContentHost.MinHeight = CompactMinimumPrimaryHeight;
            PrimaryContentHost.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            PrimaryContentHost.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

            SideContentHost.Height = double.NaN;
            SideContentHost.MinHeight = IsSidePanelVisible ? CompactMinimumSidePanelHeight : 0d;
            SideContentHost.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            SideContentHost.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        }

        private void ResetCompactChildSizing()
        {
            PrimaryContentHost.Height = double.NaN;
            PrimaryContentHost.MinHeight = 0d;
            PrimaryContentHost.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            PrimaryContentHost.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;

            SideContentHost.Height = double.NaN;
            SideContentHost.MinHeight = 0d;
            SideContentHost.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            SideContentHost.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        }

        private static void RemoveFromParent(Control control)
        {
            if (control.Parent is Panel parent)
            {
                parent.Children.Remove(control);
            }
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

        private void PublishSidePanelWidth()
        {
            if (double.IsFinite(_lastSidePanelWidth) &&
                _lastSidePanelWidth > 0d &&
                !ApproximatelyEqual(SidePanelWidth, _lastSidePanelWidth))
            {
                SetCurrentValue(SidePanelWidthProperty, _lastSidePanelWidth);
            }
        }

        private void PublishCompactSidePanelHeight()
        {
            if (double.IsFinite(_lastCompactSidePanelHeight) &&
                _lastCompactSidePanelHeight > 0d &&
                !ApproximatelyEqual(CompactSidePanelHeight, _lastCompactSidePanelHeight))
            {
                SetCurrentValue(CompactSidePanelHeightProperty, _lastCompactSidePanelHeight);
            }
        }

        private static bool TryGetPositiveDouble(object value, out double result)
        {
            result = value is double doubleValue ? doubleValue : 0d;
            return double.IsFinite(result) && result > 0d;
        }

        private static bool ApproximatelyEqual(double left, double right)
        {
            return System.Math.Abs(left - right) < 0.5d;
        }
    }
}
