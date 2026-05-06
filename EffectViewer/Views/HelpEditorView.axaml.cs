using Avalonia;
using Avalonia.Controls;
using EffectViewer.ViewModels;
using System.ComponentModel;

namespace EffectViewer.Views
{
    public partial class HelpEditorView : UserControl
    {
        private const double CompactLayoutWidth = 700d;
        private const double NavigationColumnWidth = 220d;
        private const double SplitterWidth = 5d;

        private HelpEditorViewModel _observedViewModel;

        public HelpEditorView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            UpdateResponsiveLayout();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            ObserveViewModel(null);
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            ObserveViewModel(DataContext as HelpEditorViewModel);
            UpdateResponsiveLayout();
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateResponsiveLayout();
        }

        private void OnDataContextChanged(object sender, System.EventArgs e)
        {
            ObserveViewModel(DataContext as HelpEditorViewModel);
            UpdateResponsiveLayout();
        }

        private void ObserveViewModel(HelpEditorViewModel viewModel)
        {
            if (ReferenceEquals(_observedViewModel, viewModel))
            {
                return;
            }

            if (_observedViewModel is not null)
            {
                _observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _observedViewModel = viewModel;

            if (_observedViewModel is not null)
            {
                _observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditorViewModelBase.UseMobileLayout))
            {
                UpdateResponsiveLayout();
            }
            else if (e.PropertyName == nameof(HelpEditorViewModel.SelectedSection))
            {
                SectionContentScrollViewer.Offset = new Vector(0, 0);
            }
        }

        private void UpdateResponsiveLayout()
        {
            bool compactLayout = IsCompactLayoutActive();
            if (compactLayout)
            {
                UpdateCompactLayout();
            }
            else
            {
                UpdateDockedLayout();
            }
        }

        private bool IsCompactLayoutActive()
        {
            return _observedViewModel?.UseMobileLayout == true ||
                Bounds.Width > 0 && Bounds.Width < CompactLayoutWidth;
        }

        private void UpdateDockedLayout()
        {
            LayoutRoot.Margin = new Thickness(24);
            LayoutRoot.RowSpacing = 16;
            HeaderTitleTextBlock.FontSize = 28;
            SectionTitleTextBlock.FontSize = 22;
            HelpBodyTextBlock.LineHeight = 22;
            SectionContentPanel.Margin = new Thickness(24);
            SectionContentPanel.MaxWidth = 920;
            SectionsListBox.MaxHeight = double.PositiveInfinity;
            SectionsListBox.IsVisible = true;
            SectionSelectorComboBox.IsVisible = false;

            ConfigureRow(HelpContentGrid.RowDefinitions[0], GridLength.Star, minHeight: 0d);
            ConfigureRow(HelpContentGrid.RowDefinitions[1], new GridLength(0), minHeight: 0d);
            ConfigureRow(HelpContentGrid.RowDefinitions[2], new GridLength(0), minHeight: 0d);
            ConfigureColumn(HelpContentGrid.ColumnDefinitions[0], new GridLength(NavigationColumnWidth), minWidth: 180d);
            ConfigureColumn(HelpContentGrid.ColumnDefinitions[1], new GridLength(SplitterWidth), minWidth: SplitterWidth);
            ConfigureColumn(HelpContentGrid.ColumnDefinitions[2], GridLength.Star, minWidth: 260d);

            Grid.SetRow(SectionNavigationBorder, 0);
            Grid.SetColumn(SectionNavigationBorder, 0);
            Grid.SetRowSpan(SectionNavigationBorder, 1);
            Grid.SetColumnSpan(SectionNavigationBorder, 1);
            Grid.SetRow(SectionSplitter, 0);
            Grid.SetColumn(SectionSplitter, 1);
            Grid.SetRowSpan(SectionSplitter, 1);
            Grid.SetColumnSpan(SectionSplitter, 1);
            Grid.SetRow(SectionContentScrollViewer, 0);
            Grid.SetColumn(SectionContentScrollViewer, 2);
            Grid.SetRowSpan(SectionContentScrollViewer, 1);
            Grid.SetColumnSpan(SectionContentScrollViewer, 1);

            SectionNavigationBorder.BorderThickness = new Thickness(0, 0, 1, 0);
            SectionSplitter.IsVisible = true;
            SectionSplitter.ResizeDirection = GridResizeDirection.Columns;
            SetSplitterOrientationClasses(horizontal: false);
        }

        private void UpdateCompactLayout()
        {
            LayoutRoot.Margin = new Thickness(12);
            LayoutRoot.RowSpacing = 10;
            HeaderTitleTextBlock.FontSize = 22;
            SectionTitleTextBlock.FontSize = 18;
            HelpBodyTextBlock.LineHeight = 21;
            SectionContentPanel.Margin = new Thickness(14);
            SectionContentPanel.MaxWidth = double.PositiveInfinity;
            SectionsListBox.IsVisible = false;
            SectionSelectorComboBox.IsVisible = true;

            ConfigureRow(HelpContentGrid.RowDefinitions[0], GridLength.Auto, minHeight: 0d);
            ConfigureRow(HelpContentGrid.RowDefinitions[1], new GridLength(0), minHeight: 0d);
            ConfigureRow(HelpContentGrid.RowDefinitions[2], GridLength.Star, minHeight: 0d);
            ConfigureColumn(HelpContentGrid.ColumnDefinitions[0], GridLength.Star, minWidth: 0d);
            ConfigureColumn(HelpContentGrid.ColumnDefinitions[1], new GridLength(0), minWidth: 0d);
            ConfigureColumn(HelpContentGrid.ColumnDefinitions[2], new GridLength(0), minWidth: 0d);

            Grid.SetRow(SectionNavigationBorder, 0);
            Grid.SetColumn(SectionNavigationBorder, 0);
            Grid.SetRowSpan(SectionNavigationBorder, 1);
            Grid.SetColumnSpan(SectionNavigationBorder, 3);
            Grid.SetRow(SectionSplitter, 1);
            Grid.SetColumn(SectionSplitter, 0);
            Grid.SetRowSpan(SectionSplitter, 1);
            Grid.SetColumnSpan(SectionSplitter, 3);
            Grid.SetRow(SectionContentScrollViewer, 2);
            Grid.SetColumn(SectionContentScrollViewer, 0);
            Grid.SetRowSpan(SectionContentScrollViewer, 1);
            Grid.SetColumnSpan(SectionContentScrollViewer, 3);

            SectionNavigationBorder.BorderThickness = new Thickness(0, 0, 0, 1);
            SectionSplitter.IsVisible = false;
            SectionSplitter.ResizeDirection = GridResizeDirection.Rows;
            SetSplitterOrientationClasses(horizontal: true);
        }

        private static void ConfigureColumn(ColumnDefinition column, GridLength width, double minWidth)
        {
            column.MinWidth = minWidth;
            column.Width = width;
        }

        private static void ConfigureRow(RowDefinition row, GridLength height, double minHeight)
        {
            row.MinHeight = minHeight;
            row.Height = height;
        }

        private void SetSplitterOrientationClasses(bool horizontal)
        {
            SetClass(SectionSplitter, "horizontal-splitter", horizontal);
            SetClass(SectionSplitter, "vertical-splitter", !horizontal);
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
