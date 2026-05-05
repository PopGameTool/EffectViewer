using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EffectViewer.Localization;
using EffectViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EffectViewer.Views
{
    public partial class MainView : UserControl
    {
        public static Action<bool> BrowserDialogOverlayActiveChanged { get; set; }

        private const double DefaultProjectExplorerWidth = 280d;
        private const double MinimumProjectExplorerWidth = 180d;
        private const double SplitterWidth = 5d;
        private const double CompactLayoutWidth = 700d;
        private const double CompactProjectExplorerMaximumWidth = 360d;
        private const double CompactProjectExplorerMaximumWidthRatio = 0.88d;
        private const double CompactProjectExplorerTopOffset = 42d;
        private const double TopMenuScrollDragThreshold = 5d;
        private const double DocumentTabDragThreshold = 6d;
        private double _lastLeftProjectExplorerWidth = DefaultProjectExplorerWidth;
        private double _lastRightProjectExplorerWidth = DefaultProjectExplorerWidth;
        private Point _topMenuScrollDragStartPoint;
        private Vector _topMenuScrollDragStartOffset;
        private bool _wasCompactProjectExplorerLayout;
        private bool _isTopMenuScrollPointerDown;
        private bool _isTopMenuScrollDragging;
        private Control _draggedDocumentTab;
        private EditorViewModelBase _draggedDocumentEditor;
        private EditorViewModelBase _documentTabDropTarget;
        private Point _documentTabDragStartPoint;
        private Point _documentTabDragPreviewOffset;
        private IInputPane _inputPane;
        private bool _documentTabDropAfter;
        private bool _isDocumentTabDragging;
        private MainViewModel _observedViewModel;
        private bool _browserDialogOverlayActive;

        public MainView()
        {
            InitializeComponent();
            TopMenuScrollViewer.AddHandler(PointerPressedEvent, TopMenuScrollViewer_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            TopMenuScrollViewer.AddHandler(PointerMovedEvent, TopMenuScrollViewer_PointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            TopMenuScrollViewer.AddHandler(PointerReleasedEvent, TopMenuScrollViewer_PointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
            TopMenuScrollViewer.PointerCaptureLost += TopMenuScrollViewer_PointerCaptureLost;
            DataContextChanged += OnDataContextChanged;
        }

        private async Task<T> RunStoragePickerAsync<T>(Func<Task<T>> picker)
        {
            IInputElement previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

            try
            {
                return await picker();
            }
            finally
            {
                RestoreKeyboardFocus(previousFocus);
            }
        }

        private void RestoreKeyboardFocus(IInputElement previousFocus)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (TryFocus(previousFocus))
                {
                    return;
                }

                TryFocus(this);
            }, DispatcherPriority.Input);
        }

        private static bool TryFocus(IInputElement element)
        {
            if (element is not { IsEffectivelyEnabled: true, IsEffectivelyVisible: true })
            {
                return false;
            }

            try
            {
                element.Focus(NavigationMethod.Unspecified, KeyModifiers.None);
                return element.IsFocused || element.IsKeyboardFocusWithin;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private void TopMenuScrollViewer_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(TopMenuScrollViewer).Properties.IsLeftButtonPressed)
            {
                return;
            }

            _topMenuScrollDragStartPoint = e.GetPosition(TopMenuScrollViewer);
            _topMenuScrollDragStartOffset = TopMenuScrollViewer.Offset;
            _isTopMenuScrollPointerDown = true;
            _isTopMenuScrollDragging = false;
        }

        private void TopMenuScrollViewer_PointerMoved(object sender, PointerEventArgs e)
        {
            if (!_isTopMenuScrollPointerDown)
            {
                return;
            }

            if (!e.GetCurrentPoint(TopMenuScrollViewer).Properties.IsLeftButtonPressed)
            {
                EndTopMenuScrollDrag(e);
                return;
            }

            Point currentPoint = e.GetPosition(TopMenuScrollViewer);
            if (!_isTopMenuScrollDragging &&
                GetDragDistance(_topMenuScrollDragStartPoint, currentPoint) < TopMenuScrollDragThreshold)
            {
                return;
            }

            if (!_isTopMenuScrollDragging)
            {
                _isTopMenuScrollDragging = true;
                e.Pointer.Capture(TopMenuScrollViewer);
            }

            double deltaX = currentPoint.X - _topMenuScrollDragStartPoint.X;
            double maximumOffsetX = System.Math.Max(0d, TopMenuScrollViewer.Extent.Width - TopMenuScrollViewer.Viewport.Width);
            double nextOffsetX = System.Math.Clamp(_topMenuScrollDragStartOffset.X - deltaX, 0d, maximumOffsetX);
            TopMenuScrollViewer.Offset = new Vector(nextOffsetX, TopMenuScrollViewer.Offset.Y);
            e.Handled = true;
        }

        private void TopMenuScrollViewer_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            bool wasDragging = _isTopMenuScrollDragging;
            EndTopMenuScrollDrag(e);
            e.Handled = wasDragging;
        }

        private void TopMenuScrollViewer_PointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
        {
            _isTopMenuScrollPointerDown = false;
            _isTopMenuScrollDragging = false;
        }

        private void EndTopMenuScrollDrag(PointerEventArgs e)
        {
            if (_isTopMenuScrollDragging)
            {
                e.Pointer.Capture(null);
            }

            _isTopMenuScrollPointerDown = false;
            _isTopMenuScrollDragging = false;
        }

        private void OnDataContextChanged(object sender, System.EventArgs e)
        {
            if (_observedViewModel is not null)
            {
                _observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _observedViewModel.ImportResourceFileRequested -= OnImportResourceFileRequested;
                _observedViewModel.ImportResourceFolderRequested -= OnImportResourceFolderRequested;
                _observedViewModel.ImportResourcePakRequested -= OnImportResourcePakRequested;
                _observedViewModel.ImportProjectZipRequested -= OnImportProjectZipRequested;
                _observedViewModel.ExportProjectZipRequested -= OnExportProjectZipRequested;
                _observedViewModel.ExportFileRequested -= OnExportFileRequested;
                _observedViewModel.PreviewExportRequested -= OnPreviewExportRequested;
                _observedViewModel.LoadLanguageFileRequested -= OnLoadLanguageFileRequested;
                _observedViewModel.ProjectExplorerResourceOpened -= OnProjectExplorerResourceOpened;
                _observedViewModel.RecentlyOpenedProjectItems.CollectionChanged -= OnRecentlyOpenedProjectItemsChanged;
            }

            _observedViewModel = DataContext as MainViewModel;
            _wasCompactProjectExplorerLayout = false;
            if (_observedViewModel is not null)
            {
                _lastLeftProjectExplorerWidth = NormalizeProjectExplorerWidth(_observedViewModel.ProjectExplorerLeftWidth);
                _lastRightProjectExplorerWidth = NormalizeProjectExplorerWidth(_observedViewModel.ProjectExplorerRightWidth);
                _observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
                _observedViewModel.ImportResourceFileRequested += OnImportResourceFileRequested;
                _observedViewModel.ImportResourceFolderRequested += OnImportResourceFolderRequested;
                _observedViewModel.ImportResourcePakRequested += OnImportResourcePakRequested;
                _observedViewModel.ImportProjectZipRequested += OnImportProjectZipRequested;
                _observedViewModel.ExportProjectZipRequested += OnExportProjectZipRequested;
                _observedViewModel.ExportFileRequested += OnExportFileRequested;
                _observedViewModel.PreviewExportRequested += OnPreviewExportRequested;
                _observedViewModel.LoadLanguageFileRequested += OnLoadLanguageFileRequested;
                _observedViewModel.ProjectExplorerResourceOpened += OnProjectExplorerResourceOpened;
                _observedViewModel.RecentlyOpenedProjectItems.CollectionChanged += OnRecentlyOpenedProjectItemsChanged;
            }

            RefreshRecentlyOpenedMenuItems();
            UpdateProjectExplorerLayout();
            UpdateBrowserDialogOverlayState();
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainViewModel.IsProjectExplorerVisibleLeft)
                or nameof(MainViewModel.IsProjectExplorerVisibleRight)
                or nameof(MainViewModel.IsProjectExplorerVisible)
                or nameof(MainViewModel.UseMobileLayoutOnWideScreens))
            {
                UpdateProjectExplorerLayout();
            }
            else if (e.PropertyName == nameof(MainViewModel.LayoutResetRevision))
            {
                ResetProjectExplorerWidths();
                UpdateProjectExplorerLayout(captureCurrentWidth: false);
            }
            else if (IsDialogStateProperty(e.PropertyName))
            {
                UpdateBrowserDialogOverlayState();
                RestoreKeyboardFocusIfNeeded();
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            DetachInputPane();
            SetBrowserDialogOverlayActive(false);
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            AttachInputPane();
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            AttachInputPane();
            UpdateProjectExplorerLayout();
            UpdateInputPaneMargin();
        }

        private void AttachInputPane()
        {
            if (!UsesInputPaneAvoidance())
            {
                return;
            }

            IInputPane inputPane = TopLevel.GetTopLevel(this)?.InputPane;
            if (ReferenceEquals(_inputPane, inputPane))
            {
                return;
            }

            DetachInputPane();
            _inputPane = inputPane;
            if (_inputPane is null)
            {
                return;
            }

            _inputPane.StateChanged += InputPane_StateChanged;
            UpdateInputPaneMargin();
        }

        private void DetachInputPane()
        {
            if (_inputPane is not null)
            {
                _inputPane.StateChanged -= InputPane_StateChanged;
                _inputPane = null;
            }

            Margin = default;
        }

        private void InputPane_StateChanged(object sender, InputPaneStateEventArgs e)
        {
            UpdateInputPaneMargin();
        }

        private void UpdateInputPaneMargin()
        {
            if (_inputPane is null || !UsesInputPaneAvoidance())
            {
                return;
            }

            double bottomInset = 0d;
            if (_inputPane.State == InputPaneState.Open &&
                TopLevel.GetTopLevel(this)?.ClientSize is { Height: > 0 } clientSize)
            {
                Rect occludedRect = _inputPane.OccludedRect;
                bottomInset = Math.Max(0d, clientSize.Height - occludedRect.Top);
            }

            Margin = bottomInset > 0d
                ? new Thickness(0d, 0d, 0d, bottomInset)
                : default;
        }

        private static bool UsesInputPaneAvoidance()
        {
            return OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();
        }

        private void UpdateBrowserDialogOverlayState()
        {
            if (!OperatingSystem.IsBrowser())
            {
                return;
            }

            SetBrowserDialogOverlayActive(_observedViewModel is not null && HasOpenDialog(_observedViewModel));
        }

        private void SetBrowserDialogOverlayActive(bool isActive)
        {
            if (_browserDialogOverlayActive == isActive)
            {
                return;
            }

            _browserDialogOverlayActive = isActive;
            BrowserDialogOverlayActiveChanged?.Invoke(isActive);
        }

        private void RestoreKeyboardFocusIfNeeded()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_observedViewModel is null || HasOpenDialog(_observedViewModel))
                {
                    return;
                }

                IInputElement focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                if (focusedElement is { IsEffectivelyEnabled: true, IsEffectivelyVisible: true })
                {
                    return;
                }

                TryFocus(this);
            }, DispatcherPriority.Input);
        }

        private static bool IsDialogStateProperty(string propertyName)
        {
            return propertyName is nameof(MainViewModel.IsUnsavedChangesPromptOpen)
                or nameof(MainViewModel.IsOpenProjectDialogOpen)
                or nameof(MainViewModel.IsRenameProjectDialogOpen)
                or nameof(MainViewModel.IsDeleteProjectDialogOpen)
                or nameof(MainViewModel.IsDeleteResourceDialogOpen)
                or nameof(MainViewModel.IsNewProjectDialogOpen)
                or nameof(MainViewModel.IsNewResourceDialogOpen)
                or nameof(MainViewModel.IsPreviewExportDialogOpen)
                or nameof(MainViewModel.IsProjectTransferInProgress);
        }

        private static bool HasOpenDialog(MainViewModel viewModel)
        {
            return viewModel.IsUnsavedChangesPromptOpen
                || viewModel.IsOpenProjectDialogOpen
                || viewModel.IsRenameProjectDialogOpen
                || viewModel.IsDeleteProjectDialogOpen
                || viewModel.IsDeleteResourceDialogOpen
                || viewModel.IsNewProjectDialogOpen
                || viewModel.IsNewResourceDialogOpen
                || viewModel.IsPreviewExportDialogOpen
                || viewModel.IsProjectTransferInProgress;
        }

        private void OnRecentlyOpenedProjectItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshRecentlyOpenedMenuItems();
        }

        private void OnProjectExplorerResourceOpened(object sender, EventArgs e)
        {
            if (Bounds.Width <= 0 ||
                !IsCompactLayoutActive() ||
                _observedViewModel?.IsProjectExplorerVisible != true)
            {
                return;
            }

            _observedViewModel.IsProjectExplorerVisible = false;
        }

        private void RefreshRecentlyOpenedMenuItems()
        {
            RecentlyOpenedMenuItem.Items.Clear();

            if (_observedViewModel is null)
            {
                return;
            }

            foreach (ProjectExplorerItemViewModel item in _observedViewModel.RecentlyOpenedProjectItems)
            {
                RecentlyOpenedMenuItem.Items.Add(new MenuItem
                {
                    Header = item.Title,
                    Command = _observedViewModel.OpenProjectExplorerItemCommand,
                    CommandParameter = item
                });
            }
        }

        private void CaptureVisibleProjectExplorerSize(bool compactLayout)
        {
            if (compactLayout)
            {
                return;
            }

            ColumnDefinition leftColumn = WorkspaceGrid.ColumnDefinitions[0];
            ColumnDefinition rightColumn = WorkspaceGrid.ColumnDefinitions[4];

            if (_observedViewModel?.IsProjectExplorerVisibleLeft == true && leftColumn.ActualWidth > 0)
            {
                _lastLeftProjectExplorerWidth = leftColumn.ActualWidth;
                _observedViewModel.ProjectExplorerLeftWidth = _lastLeftProjectExplorerWidth;
            }
            else if (_observedViewModel?.IsProjectExplorerVisibleRight == true && rightColumn.ActualWidth > 0)
            {
                _lastRightProjectExplorerWidth = rightColumn.ActualWidth;
                _observedViewModel.ProjectExplorerRightWidth = _lastRightProjectExplorerWidth;
            }
        }

        private void UpdateProjectExplorerLayout(bool captureCurrentWidth = true)
        {
            bool compactLayout = IsCompactLayoutActive();
            ApplyCompactProjectExplorerState(compactLayout);
            CompactProjectExplorerButton.IsVisible = compactLayout;

            if (captureCurrentWidth)
            {
                CaptureVisibleProjectExplorerSize(compactLayout);
            }

            bool showLeft = _observedViewModel?.IsProjectExplorerVisibleLeft == true;
            bool showRight = _observedViewModel?.IsProjectExplorerVisibleRight == true;

            if (compactLayout)
            {
                UpdateCompactProjectExplorerLayout(showLeft, showRight);
                return;
            }

            UpdateDockedProjectExplorerLayout(showLeft, showRight);
        }

        private bool IsCompactLayoutActive()
        {
            return _observedViewModel?.UseMobileLayoutOnWideScreens == true ||
                Bounds.Width > 0 && Bounds.Width < CompactLayoutWidth;
        }

        private void UpdateDockedProjectExplorerLayout(bool showLeft, bool showRight)
        {
            ResetProjectExplorerOverlayLayout();
            ConfigureRow(WorkspaceGrid.RowDefinitions[0], 1d, 0d, GridUnitType.Star);
            ConfigureRow(WorkspaceGrid.RowDefinitions[1], 0d, 0d);
            ConfigureRow(WorkspaceGrid.RowDefinitions[2], 0d, 0d);
            ConfigureColumn(WorkspaceGrid.ColumnDefinitions[2], 1d, 320d, GridUnitType.Star);

            Grid.SetRow(LeftProjectExplorerPanel, 0);
            Grid.SetRow(LeftProjectExplorerSplitter, 0);
            Grid.SetRow(WorkspaceContentGrid, 0);
            Grid.SetRow(RightProjectExplorerSplitter, 0);
            Grid.SetRow(RightProjectExplorerPanel, 0);
            Grid.SetColumn(LeftProjectExplorerPanel, 0);
            Grid.SetColumn(LeftProjectExplorerSplitter, 1);
            Grid.SetColumn(WorkspaceContentGrid, 2);
            Grid.SetColumn(RightProjectExplorerSplitter, 3);
            Grid.SetColumn(RightProjectExplorerPanel, 4);
            SetGridSpans(LeftProjectExplorerPanel, columnSpan: 1, rowSpan: 1);
            SetGridSpans(LeftProjectExplorerSplitter, columnSpan: 1, rowSpan: 1);
            SetGridSpans(WorkspaceContentGrid, columnSpan: 1, rowSpan: 1);
            SetGridSpans(RightProjectExplorerSplitter, columnSpan: 1, rowSpan: 1);
            SetGridSpans(RightProjectExplorerPanel, columnSpan: 1, rowSpan: 1);
            ConfigureProjectExplorerSplitter(LeftProjectExplorerSplitter, horizontal: false);
            ConfigureProjectExplorerSplitter(RightProjectExplorerSplitter, horizontal: false);
            LeftProjectExplorerPanel.BorderThickness = new Thickness(0, 0, 1, 0);
            RightProjectExplorerPanel.BorderThickness = new Thickness(1, 0, 0, 0);

            ConfigureColumn(
                WorkspaceGrid.ColumnDefinitions[0],
                showLeft ? _lastLeftProjectExplorerWidth : 0d,
                showLeft ? MinimumProjectExplorerWidth : 0d);
            ConfigureColumn(
                WorkspaceGrid.ColumnDefinitions[1],
                showLeft ? SplitterWidth : 0d,
                showLeft ? SplitterWidth : 0d);
            ConfigureColumn(
                WorkspaceGrid.ColumnDefinitions[3],
                showRight ? SplitterWidth : 0d,
                showRight ? SplitterWidth : 0d);
            ConfigureColumn(
                WorkspaceGrid.ColumnDefinitions[4],
                showRight ? _lastRightProjectExplorerWidth : 0d,
                showRight ? MinimumProjectExplorerWidth : 0d);
        }

        private void UpdateCompactProjectExplorerLayout(bool showLeft, bool showRight)
        {
            bool showExplorer = showLeft || showRight;

            ConfigureRow(WorkspaceGrid.RowDefinitions[0], 1d, 0d, GridUnitType.Star);
            ConfigureRow(WorkspaceGrid.RowDefinitions[1], 0d, 0d);
            ConfigureRow(WorkspaceGrid.RowDefinitions[2], 0d, 0d);

            ConfigureColumn(WorkspaceGrid.ColumnDefinitions[0], 1d, 0d, GridUnitType.Star);
            ConfigureColumn(WorkspaceGrid.ColumnDefinitions[1], 0d, 0d);
            ConfigureColumn(WorkspaceGrid.ColumnDefinitions[2], 0d, 0d);
            ConfigureColumn(WorkspaceGrid.ColumnDefinitions[3], 0d, 0d);
            ConfigureColumn(WorkspaceGrid.ColumnDefinitions[4], 0d, 0d);

            Grid.SetRow(LeftProjectExplorerPanel, 0);
            Grid.SetRow(RightProjectExplorerPanel, 0);
            Grid.SetRow(LeftProjectExplorerSplitter, 0);
            Grid.SetRow(RightProjectExplorerSplitter, 0);
            Grid.SetRow(WorkspaceContentGrid, 0);
            Grid.SetColumn(LeftProjectExplorerPanel, 0);
            Grid.SetColumn(RightProjectExplorerPanel, 0);
            Grid.SetColumn(LeftProjectExplorerSplitter, 0);
            Grid.SetColumn(RightProjectExplorerSplitter, 0);
            Grid.SetColumn(WorkspaceContentGrid, 0);
            SetGridSpans(LeftProjectExplorerPanel, columnSpan: 5, rowSpan: 1);
            SetGridSpans(RightProjectExplorerPanel, columnSpan: 5, rowSpan: 1);
            SetGridSpans(LeftProjectExplorerSplitter, columnSpan: 5, rowSpan: 1);
            SetGridSpans(RightProjectExplorerSplitter, columnSpan: 5, rowSpan: 1);
            SetGridSpans(WorkspaceContentGrid, columnSpan: 5, rowSpan: 1);
            ConfigureProjectExplorerSplitter(LeftProjectExplorerSplitter, horizontal: true);
            ConfigureProjectExplorerSplitter(RightProjectExplorerSplitter, horizontal: true);
            LeftProjectExplorerPanel.BorderThickness = new Thickness(0, 0, 0, 1);
            RightProjectExplorerPanel.BorderThickness = new Thickness(0, 0, 0, 1);
            ApplyProjectExplorerOverlayLayout(showExplorer);
        }

        private void ResetProjectExplorerWidths()
        {
            _lastLeftProjectExplorerWidth = DefaultProjectExplorerWidth;
            _lastRightProjectExplorerWidth = DefaultProjectExplorerWidth;
            if (_observedViewModel is not null)
            {
                _observedViewModel.ProjectExplorerLeftWidth = DefaultProjectExplorerWidth;
                _observedViewModel.ProjectExplorerRightWidth = DefaultProjectExplorerWidth;
            }
        }

        private void ApplyCompactProjectExplorerState(bool compactLayout)
        {
            if (!compactLayout)
            {
                _wasCompactProjectExplorerLayout = false;
                return;
            }

            if (_observedViewModel is null)
            {
                _wasCompactProjectExplorerLayout = true;
                return;
            }

            if (!_wasCompactProjectExplorerLayout)
            {
                _observedViewModel.ApplyTemporaryProjectExplorerLayout(ProjectExplorerDockSide.Left, isVisible: false);
            }
            else if (_observedViewModel.IsProjectExplorerVisibleRight)
            {
                _observedViewModel.ApplyTemporaryProjectExplorerLayout(ProjectExplorerDockSide.Left, _observedViewModel.IsProjectExplorerVisible);
            }

            _wasCompactProjectExplorerLayout = true;
        }

        private void ApplyProjectExplorerOverlayLayout(bool showExplorer)
        {
            double availableWidth = Bounds.Width > 0 ? Bounds.Width : DefaultProjectExplorerWidth;
            double maximumWidth = System.Math.Max(
                MinimumProjectExplorerWidth,
                System.Math.Min(
                    CompactProjectExplorerMaximumWidth,
                    availableWidth * CompactProjectExplorerMaximumWidthRatio));
            double overlayWidth = showExplorer
                ? System.Math.Min(System.Math.Max(MinimumProjectExplorerWidth, _lastLeftProjectExplorerWidth), maximumWidth)
                : 0d;

            ConfigureOverlayPanel(LeftProjectExplorerPanel, overlayWidth, maximumWidth);
            ConfigureOverlayPanel(RightProjectExplorerPanel, overlayWidth, maximumWidth);
            ConfigureOverlayDismissArea(overlayWidth, showExplorer);
            SetZIndexes(projectExplorerZIndex: 20);
        }

        private static void ConfigureOverlayPanel(Control panel, double width, double maximumWidth)
        {
            panel.Width = width;
            panel.MaxWidth = maximumWidth;
            panel.Margin = new Thickness(0, CompactProjectExplorerTopOffset, 0, 0);
            panel.HorizontalAlignment = HorizontalAlignment.Left;
            panel.VerticalAlignment = VerticalAlignment.Stretch;
        }

        private void ConfigureOverlayDismissArea(double projectExplorerWidth, bool isVisible)
        {
            Grid.SetRow(CompactProjectExplorerDismissOverlay, 0);
            Grid.SetColumn(CompactProjectExplorerDismissOverlay, 0);
            SetGridSpans(CompactProjectExplorerDismissOverlay, columnSpan: 5, rowSpan: 1);

            CompactProjectExplorerDismissOverlay.Margin = new Thickness(
                projectExplorerWidth,
                CompactProjectExplorerTopOffset,
                0d,
                0d);
            CompactProjectExplorerDismissOverlay.HorizontalAlignment = HorizontalAlignment.Stretch;
            CompactProjectExplorerDismissOverlay.VerticalAlignment = VerticalAlignment.Stretch;
            CompactProjectExplorerDismissOverlay.IsVisible = isVisible;
        }

        private void ResetProjectExplorerOverlayLayout()
        {
            ResetProjectExplorerOverlayPanel(LeftProjectExplorerPanel);
            ResetProjectExplorerOverlayPanel(RightProjectExplorerPanel);
            ResetProjectExplorerOverlayPanel(CompactProjectExplorerDismissOverlay);
            CompactProjectExplorerDismissOverlay.IsVisible = false;
            SetZIndexes(projectExplorerZIndex: 0);
        }

        private static void ResetProjectExplorerOverlayPanel(Control panel)
        {
            panel.Width = double.NaN;
            panel.MaxWidth = double.PositiveInfinity;
            panel.Margin = new Thickness(0);
            panel.HorizontalAlignment = HorizontalAlignment.Stretch;
            panel.VerticalAlignment = VerticalAlignment.Stretch;
        }

        private void SetZIndexes(int projectExplorerZIndex)
        {
            WorkspaceContentGrid.ZIndex = 0;
            LeftProjectExplorerPanel.ZIndex = projectExplorerZIndex;
            RightProjectExplorerPanel.ZIndex = projectExplorerZIndex;
            CompactProjectExplorerDismissOverlay.ZIndex = projectExplorerZIndex > 0 ? projectExplorerZIndex - 1 : 0;
            LeftProjectExplorerSplitter.ZIndex = 0;
            RightProjectExplorerSplitter.ZIndex = 0;
        }

        private void CompactProjectExplorerDismissOverlay_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel && IsCompactLayoutActive())
            {
                viewModel.IsProjectExplorerVisible = false;
                UpdateProjectExplorerLayout(captureCurrentWidth: false);
                e.Handled = true;
            }
        }

        private void CompactProjectExplorerButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel viewModel)
            {
                return;
            }

            if (viewModel.IsProjectExplorerVisibleLeft)
            {
                viewModel.IsProjectExplorerVisible = false;
            }
            else
            {
                viewModel.ProjectExplorerDockSide = ProjectExplorerDockSide.Left;
                viewModel.IsProjectExplorerVisible = true;
            }

            UpdateProjectExplorerLayout(captureCurrentWidth: false);
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

        private static double NormalizeProjectExplorerWidth(double value)
        {
            return double.IsFinite(value) && value > 0d ? value : DefaultProjectExplorerWidth;
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

        private static void SetGridSpans(Control control, int columnSpan, int rowSpan)
        {
            Grid.SetColumnSpan(control, columnSpan);
            Grid.SetRowSpan(control, rowSpan);
        }

        private static void ConfigureProjectExplorerSplitter(GridSplitter splitter, bool horizontal)
        {
            splitter.ResizeDirection = horizontal ? GridResizeDirection.Rows : GridResizeDirection.Columns;
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

        private void DocumentTab_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is not Control tab ||
                tab.DataContext is not EditorViewModelBase editor ||
                !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
                IsCloseButtonPointer(e))
            {
                return;
            }

            if (DataContext is MainViewModel viewModel)
            {
                viewModel.SelectedEditor = editor;
            }

            _draggedDocumentTab = tab;
            _draggedDocumentEditor = editor;
            _documentTabDragStartPoint = e.GetPosition(this);
            _documentTabDragPreviewOffset = e.GetPosition(tab);
            _isDocumentTabDragging = false;
            e.Pointer.Capture(tab);
            e.Handled = true;
        }

        private void DocumentTab_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_draggedDocumentTab is null)
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                EndDocumentTabDrag(e);
                return;
            }

            Point currentPoint = e.GetPosition(this);
            if (!_isDocumentTabDragging &&
                GetDragDistance(_documentTabDragStartPoint, currentPoint) >= DocumentTabDragThreshold)
            {
                _isDocumentTabDragging = true;
                _draggedDocumentEditor.IsTabDragging = true;
                ShowDocumentTabDragPreview();
            }

            if (_isDocumentTabDragging)
            {
                UpdateDocumentTabDragPreviewPosition(currentPoint);
                UpdateDocumentTabDropTarget(e);
            }

            e.Handled = _isDocumentTabDragging;
        }

        private void DocumentTab_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_draggedDocumentTab is not null &&
                _isDocumentTabDragging &&
                DataContext is MainViewModel viewModel)
            {
                UpdateDocumentTabDropTarget(e);
                if (_documentTabDropTarget is not null)
                {
                    viewModel.ReorderEditorTab(_draggedDocumentEditor, _documentTabDropTarget, _documentTabDropAfter);
                }
            }

            EndDocumentTabDrag(e);
            e.Handled = true;
        }

        private void DocumentTab_PointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
        {
            if (_draggedDocumentTab is null)
            {
                return;
            }

            ClearDocumentTabDragVisuals();
            _draggedDocumentTab = null;
            _draggedDocumentEditor = null;
            _documentTabDropTarget = null;
            _documentTabDropAfter = false;
            _isDocumentTabDragging = false;
        }

        private void EndDocumentTabDrag(PointerEventArgs e)
        {
            e.Pointer.Capture(null);
            ClearDocumentTabDragVisuals();
            _draggedDocumentTab = null;
            _draggedDocumentEditor = null;
            _documentTabDropTarget = null;
            _documentTabDropAfter = false;
            _isDocumentTabDragging = false;
        }

        private void ShowDocumentTabDragPreview()
        {
            if (_draggedDocumentTab is null || _draggedDocumentEditor is null)
            {
                return;
            }

            DocumentTabDragPreview.Width = System.Math.Clamp(_draggedDocumentTab.Bounds.Width, 150d, 320d);
            DocumentTabDragPreview.Height = _draggedDocumentTab.Bounds.Height;
            DocumentTabDragPreviewKind.Text = _draggedDocumentEditor.KindCode;
            DocumentTabDragPreviewTitle.Text = _draggedDocumentEditor.TabTitle;
            DocumentTabDragPreviewPin.IsVisible = _draggedDocumentEditor.IsPinned;
            DocumentTabDragPreview.IsVisible = true;
            UpdateDocumentTabDragPreviewPosition(_documentTabDragStartPoint);
        }

        private void UpdateDocumentTabDragPreviewPosition(Point point)
        {
            if (!DocumentTabDragPreview.IsVisible)
            {
                return;
            }

            Canvas.SetLeft(DocumentTabDragPreview, point.X - _documentTabDragPreviewOffset.X);
            Canvas.SetTop(DocumentTabDragPreview, point.Y - _documentTabDragPreviewOffset.Y);
        }

        private void UpdateDocumentTabDropTarget(PointerEventArgs e)
        {
            Point point = e.GetPosition(this);
            if (TryFindDocumentTabAt(point, out Control targetTab) &&
                targetTab.DataContext is EditorViewModelBase targetEditor)
            {
                bool insertAfter = e.GetPosition(targetTab).X > targetTab.Bounds.Width / 2d;
                SetDocumentTabDropTarget(targetEditor, insertAfter);
                return;
            }

            if (TryFindDocumentTabStripEdgeTarget(point, out targetEditor, out bool insertAfterAtEdge))
            {
                SetDocumentTabDropTarget(targetEditor, insertAfterAtEdge);
                return;
            }

            ClearDocumentTabDropTarget();
        }

        private void SetDocumentTabDropTarget(EditorViewModelBase targetEditor, bool insertAfter)
        {
            if (ReferenceEquals(targetEditor, _draggedDocumentEditor))
            {
                ClearDocumentTabDropTarget();
                return;
            }

            if (!ReferenceEquals(_documentTabDropTarget, targetEditor))
            {
                ClearDocumentTabDropTarget();
                _documentTabDropTarget = targetEditor;
            }

            _documentTabDropAfter = insertAfter;
            targetEditor.IsTabDropBefore = !insertAfter;
            targetEditor.IsTabDropAfter = insertAfter;
        }

        private bool TryFindDocumentTabStripEdgeTarget(
            Point point,
            out EditorViewModelBase targetEditor,
            out bool insertAfter)
        {
            targetEditor = null;
            insertAfter = false;

            if (!IsPointInsideDocumentTabStrip(point))
            {
                return false;
            }

            List<DocumentTabBounds> tabs = GetDocumentTabBounds();
            if (tabs.Count == 0)
            {
                return false;
            }

            DocumentTabBounds first = tabs[0];
            DocumentTabBounds last = tabs[^1];
            if (point.X <= first.Left)
            {
                targetEditor = first.Editor;
                insertAfter = false;
                return true;
            }

            if (point.X >= last.Right)
            {
                targetEditor = last.Editor;
                insertAfter = true;
                return true;
            }

            for (int i = 0; i < tabs.Count - 1; i++)
            {
                DocumentTabBounds current = tabs[i];
                DocumentTabBounds next = tabs[i + 1];
                if (point.X >= current.Right && point.X <= next.Left)
                {
                    double midpoint = current.Right + ((next.Left - current.Right) / 2d);
                    if (point.X <= midpoint)
                    {
                        targetEditor = current.Editor;
                        insertAfter = true;
                    }
                    else
                    {
                        targetEditor = next.Editor;
                        insertAfter = false;
                    }

                    return true;
                }
            }

            return false;
        }

        private void ClearDocumentTabDragVisuals()
        {
            if (_draggedDocumentEditor is not null)
            {
                _draggedDocumentEditor.IsTabDragging = false;
            }

            DocumentTabDragPreview.IsVisible = false;
            ClearDocumentTabDropTarget();
        }

        private void ClearDocumentTabDropTarget()
        {
            if (_documentTabDropTarget is not null)
            {
                _documentTabDropTarget.IsTabDropBefore = false;
                _documentTabDropTarget.IsTabDropAfter = false;
                _documentTabDropTarget = null;
            }

            _documentTabDropAfter = false;
        }

        private bool TryFindDocumentTabAt(Point point, out Control tab)
        {
            foreach (Avalonia.Visual visual in this.GetVisualsAt(point))
            {
                tab = visual.GetSelfAndVisualAncestors()
                    .OfType<Control>()
                    .FirstOrDefault(control =>
                        control.Classes.Contains("document-tab") &&
                        control.DataContext is EditorViewModelBase);

                if (tab is not null)
                {
                    return true;
                }
            }

            tab = null;
            return false;
        }

        private bool IsPointInsideDocumentTabStrip(Point point)
        {
            Point? tabStripOrigin = DocumentTabHeaderGrid.TranslatePoint(new Point(0, 0), this);
            if (tabStripOrigin is null)
            {
                return false;
            }

            Rect tabStripBounds = new Rect(tabStripOrigin.Value, DocumentTabHeaderGrid.Bounds.Size);
            return tabStripBounds.Contains(point);
        }

        private List<DocumentTabBounds> GetDocumentTabBounds()
        {
            return DocumentTabStripScrollViewer
                .GetVisualDescendants()
                .OfType<Control>()
                .Where(control =>
                    control.Classes.Contains("document-tab") &&
                    control.DataContext is EditorViewModelBase &&
                    control.Bounds.Width > 0d)
                .Select(control =>
                {
                    Point? origin = control.TranslatePoint(new Point(0, 0), this);
                    EditorViewModelBase editor = (EditorViewModelBase)control.DataContext;
                    return origin is null
                        ? null
                        : new DocumentTabBounds(editor, origin.Value.X, origin.Value.X + control.Bounds.Width);
                })
                .Where(tab => tab is not null)
                .OrderBy(tab => tab.Left)
                .ToList();
        }

        private sealed class DocumentTabBounds
        {
            public DocumentTabBounds(EditorViewModelBase editor, double left, double right)
            {
                Editor = editor;
                Left = left;
                Right = right;
            }

            public EditorViewModelBase Editor { get; }

            public double Left { get; }

            public double Right { get; }
        }

        private static double GetDragDistance(Point start, Point current)
        {
            double deltaX = current.X - start.X;
            double deltaY = current.Y - start.Y;
            return System.Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        private static bool IsCloseButtonPointer(PointerPressedEventArgs e)
        {
            return e.Source is Avalonia.Visual visual &&
                visual.GetSelfAndVisualAncestors()
                    .OfType<Button>()
                    .Any(button => button.Classes.Contains("tab-close-button"));
        }

        private void ProjectExplorer_DragOver(object sender, DragEventArgs e)
        {
            bool canImport = CanImportDraggedData(e.DataTransfer);
            e.DragEffects = canImport ? DragDropEffects.Copy : DragDropEffects.None;
            SetProjectExplorerDragOver(sender, canImport);
            e.Handled = true;
        }

        private void ProjectExplorer_DragLeave(object sender, DragEventArgs e)
        {
            SetProjectExplorerDragOver(sender, false);
        }

        private async void ProjectExplorer_Drop(object sender, DragEventArgs e)
        {
            SetProjectExplorerDragOver(sender, false);
            e.Handled = true;

            if (DataContext is not MainViewModel viewModel)
            {
                return;
            }

            IReadOnlyList<IStorageItem> items = e.DataTransfer.TryGetFiles()?.ToList() ?? [];
            if (items.Count == 0)
            {
                viewModel.StatusText = Loc.Text("Status.DropImportUnsupported");
                return;
            }

            int importedCount = 0;
            int skippedCount = 0;
            foreach (IStorageItem item in items)
            {
                if (await TryImportDroppedItemAsync(viewModel, item))
                {
                    importedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            if (importedCount > 0 && skippedCount > 0)
            {
                viewModel.StatusText = Loc.Format("Status.ImportedDroppedItemsWithSkipped", importedCount, skippedCount);
            }
            else if (importedCount > 1)
            {
                viewModel.StatusText = Loc.Format("Status.ImportedDroppedItems", importedCount);
            }
            else if (importedCount == 0)
            {
                viewModel.StatusText = Loc.Text("Status.DropImportUnsupported");
            }
        }

        private static void SetProjectExplorerDragOver(object sender, bool isDragOver)
        {
            if (sender is Control control)
            {
                control.Classes.Set("drag-over", isDragOver);
            }
        }

        private static bool CanImportDraggedData(IDataTransfer data)
        {
            return data?.TryGetFiles()?.Any(IsPotentiallyImportableStorageItem) == true;
        }

        private static bool IsPotentiallyImportableStorageItem(IStorageItem item)
        {
            if (item is null)
            {
                return false;
            }

            if (item is IStorageFile file)
            {
                return IsSupportedResourceFileName(file.Name);
            }

            string localPath = item.TryGetLocalPath();
            return File.Exists(localPath) && IsSupportedResourceFileName(localPath);
        }

        private async Task<bool> TryImportDroppedItemAsync(MainViewModel viewModel, IStorageItem item)
        {
            if (item is null)
            {
                return false;
            }

            if (item is IStorageFile file)
            {
                if (!IsSupportedResourceFileName(file.Name))
                {
                    return false;
                }

                await using Stream resourceStream = await file.OpenReadAsync();
                if (IsPakResourceFileName(file.Name))
                {
                    await viewModel.ImportResourcePakAsync(file.Name, resourceStream);
                }
                else
                {
                    await viewModel.ImportResourceFileAsync(file.Name, resourceStream);
                }

                return true;
            }

            string localPath = item.TryGetLocalPath();
            if (!File.Exists(localPath))
            {
                return false;
            }

            if (!IsSupportedResourceFileName(localPath))
            {
                return false;
            }

            await using FileStream localResourceStream = File.OpenRead(localPath);
            string localFileName = Path.GetFileName(localPath);
            if (IsPakResourceFileName(localFileName))
            {
                await viewModel.ImportResourcePakAsync(localFileName, localResourceStream);
            }
            else
            {
                await viewModel.ImportResourceFileAsync(localFileName, localResourceStream);
            }

            return true;
        }

        private async void ImportFolderMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await ImportResourceFolderFromPickerAsync();
        }

        private async void OnImportResourceFolderRequested(object sender, System.EventArgs e)
        {
            await ImportResourceFolderFromPickerAsync();
        }

        private async void ImportPakMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await ImportResourcePakFromPickerAsync();
        }

        private async void OnImportResourcePakRequested(object sender, System.EventArgs e)
        {
            await ImportResourcePakFromPickerAsync();
        }

        private async Task ImportResourceFolderFromPickerAsync()
        {
            try
            {
                IStorageFolder folder = await PickFolderAsync(Loc.Text("FilePicker.ImportResourceFolder"));
                if (folder is not null && DataContext is MainViewModel viewModel)
                {
                    string path = folder.TryGetLocalPath();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        await viewModel.ImportResourceFolderAsync(path);
                    }
                    else
                    {
                        await viewModel.ImportResourceFolderAsync(folder);
                    }
                }
            }
            catch (System.Exception ex) when (DataContext is MainViewModel viewModel)
            {
                viewModel.StatusText = Loc.Format("Status.CouldNotImportFolder", ex.Message);
            }
        }

        private async Task ImportResourcePakFromPickerAsync()
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            var files = await RunStoragePickerAsync(() => topLevel.StorageProvider.OpenFilePickerAsync(new()
            {
                Title = Loc.Text("FilePicker.ImportResourcePak"),
                AllowMultiple = false,
                FileTypeFilter = [PakFileType]
            }));

            if (files.Count == 0)
            {
                return;
            }

            await using Stream stream = await files[0].OpenReadAsync();
            await viewModel.ImportResourcePakAsync(files[0].Name, stream);
        }

        private async void ImportProjectMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await ImportProjectZipFromPickerAsync();
        }

        private async void OnImportProjectZipRequested(object sender, System.EventArgs e)
        {
            await ImportProjectZipFromPickerAsync();
        }

        private async Task ImportProjectZipFromPickerAsync()
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            var files = await RunStoragePickerAsync(() => topLevel.StorageProvider.OpenFilePickerAsync(new()
            {
                Title = Loc.Text("FilePicker.ImportProjectZip"),
                AllowMultiple = false,
                FileTypeFilter = [ZipFileType]
            }));

            if (files.Count == 0)
            {
                return;
            }

            await using Stream stream = await files[0].OpenReadAsync();
            await viewModel.ImportProjectZipAsync(stream);
        }

        private async void ImportResourceFileMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await ImportResourceFileFromPickerAsync();
        }

        private async void OnImportResourceFileRequested(object sender, System.EventArgs e)
        {
            await ImportResourceFileFromPickerAsync();
        }

        private async Task ImportResourceFileFromPickerAsync()
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            var files = await RunStoragePickerAsync(() => topLevel.StorageProvider.OpenFilePickerAsync(new()
            {
                Title = Loc.Text("FilePicker.ImportResourceFile"),
                AllowMultiple = false,
                FileTypeFilter = [ResourceFileType]
            }));

            if (files.Count == 0)
            {
                return;
            }

            await using Stream stream = await files[0].OpenReadAsync();
            await viewModel.ImportResourceFileAsync(files[0].Name, stream);
        }

        private async void ExportProjectMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await ExportProjectZipFromPickerAsync();
        }

        private async void OnExportProjectZipRequested(object sender, System.EventArgs e)
        {
            await ExportProjectZipFromPickerAsync();
        }

        private async Task ExportProjectZipFromPickerAsync()
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            string suggestedName = CreateExportFileName(viewModel.CurrentProject?.Manifest?.Name);
            IStorageFile file = await RunStoragePickerAsync(() => topLevel.StorageProvider.SaveFilePickerAsync(new()
            {
                Title = Loc.Text("FilePicker.ExportProjectZip"),
                SuggestedFileName = suggestedName,
                DefaultExtension = "zip",
                FileTypeChoices = [ZipFileType]
            }));

            if (file is null)
            {
                return;
            }

            await using Stream stream = await file.OpenWriteAsync();
            await viewModel.ExportCurrentProjectZipAsync(stream);
        }

        private async void ExportFileMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await ExportSelectedFileFromPickerAsync();
        }

        private async void OnExportFileRequested(object sender, System.EventArgs e)
        {
            await ExportSelectedFileFromPickerAsync();
        }

        private async Task ExportSelectedFileFromPickerAsync()
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            if (!await viewModel.PrepareSelectedFileExportAsync())
            {
                return;
            }

            string suggestedName = viewModel.GetSelectedFileExportName();
            string defaultExtension = viewModel.GetSelectedFileDefaultExtension();
            IReadOnlyList<string> exportPatterns = viewModel.GetSelectedFileExportPatterns();
            List<FilePickerFileType> fileTypeChoices = exportPatterns.Count == 2
                ?
                [
                    new FilePickerFileType(Loc.Text("FilePicker.Source")) { Patterns = [exportPatterns[0]] },
                    new FilePickerFileType(Loc.Text("FilePicker.Compiled")) { Patterns = [exportPatterns[1]] }
                ]
                :
                [
                    new FilePickerFileType(Loc.Text("FilePicker.SupportedFormats")) { Patterns = [.. exportPatterns] }
                ];
            IStorageFile file = await RunStoragePickerAsync(() => topLevel.StorageProvider.SaveFilePickerAsync(new()
            {
                Title = Loc.Text("FilePicker.ExportCurrentFile"),
                SuggestedFileName = suggestedName,
                DefaultExtension = defaultExtension,
                FileTypeChoices = fileTypeChoices
            }));

            if (file is null)
            {
                return;
            }

            await using Stream stream = await file.OpenWriteAsync();
            await viewModel.ExportSelectedFileAsync(stream, file.Name);
        }

        private async void OnPreviewExportRequested(object sender, System.EventArgs e)
        {
            await ExportPreviewFromPickerAsync();
        }

        private async Task ExportPreviewFromPickerAsync()
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            IReadOnlyList<string> exportPatterns = viewModel.GetPreviewExportPatterns();
            IStorageFile file = await RunStoragePickerAsync(() => topLevel.StorageProvider.SaveFilePickerAsync(new()
            {
                Title = Loc.Text("FilePicker.ExportPreview"),
                SuggestedFileName = viewModel.GetPreviewExportFileName(),
                DefaultExtension = viewModel.GetPreviewExportDefaultExtension(),
                FileTypeChoices =
                [
                    new FilePickerFileType(Loc.Text("FilePicker.PreviewExport"))
                    {
                        Patterns = [.. exportPatterns]
                    }
                ]
            }));

            if (file is null)
            {
                return;
            }

            await using Stream stream = await file.OpenWriteAsync();
            await viewModel.ExportSelectedPreviewAsync(stream);
        }

        private async void LoadLanguageFileMenuItem_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await LoadLanguageFileFromPickerAsync();
        }

        private async void OnLoadLanguageFileRequested(object sender, System.EventArgs e)
        {
            await LoadLanguageFileFromPickerAsync();
        }

        private async Task LoadLanguageFileFromPickerAsync()
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            var files = await RunStoragePickerAsync(() => topLevel.StorageProvider.OpenFilePickerAsync(new()
            {
                Title = Loc.Text("Menu.LoadLanguageFile"),
                AllowMultiple = false,
                FileTypeFilter = [LanguageFileType]
            }));

            if (files.Count == 0)
            {
                return;
            }

            await using Stream stream = await files[0].OpenReadAsync();
            await viewModel.LoadLanguageFileAsync(stream, files[0].Name);
        }

        private async Task<IStorageFolder> PickFolderAsync(string title)
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
            {
                return null;
            }

            var folders = await RunStoragePickerAsync(() => topLevel.StorageProvider.OpenFolderPickerAsync(new()
            {
                Title = title,
                AllowMultiple = false
            }));

            return folders.Count > 0 ? folders[0] : null;
        }

        private static LocalizationManager Loc => LocalizationManager.Instance;

        private static bool IsSupportedResourceFileName(string fileName)
        {
            string lower = Path.GetFileName(fileName ?? string.Empty).ToLowerInvariant();
            return lower.EndsWith(".reanim", StringComparison.OrdinalIgnoreCase) ||
                lower.EndsWith(".reanim.compiled", StringComparison.OrdinalIgnoreCase) ||
                lower.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                lower.EndsWith(".xml.compiled", StringComparison.OrdinalIgnoreCase) ||
                lower.EndsWith(".trail", StringComparison.OrdinalIgnoreCase) ||
                lower.EndsWith(".trail.compiled", StringComparison.OrdinalIgnoreCase) ||
                lower.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ||
                lower.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                lower.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(lower) is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".tga";
        }

        private static bool IsPakResourceFileName(string fileName)
        {
            return string.Equals(Path.GetExtension(fileName ?? string.Empty), ".pak", StringComparison.OrdinalIgnoreCase);
        }

        private static FilePickerFileType ZipFileType => new(Loc.Text("FilePicker.ZipArchive"))
        {
            Patterns = ["*.zip"],
            MimeTypes = ["application/zip", "application/x-zip-compressed"]
        };

        private static FilePickerFileType PakFileType => new(Loc.Text("FilePicker.PakArchive"))
        {
            Patterns = ["*.pak"],
            MimeTypes = ["application/octet-stream"]
        };

        private static FilePickerFileType ResourceFileType => new(Loc.Text("FilePicker.EffectResources"))
        {
            Patterns =
            [
                "*.png",
                "*.jpg",
                "*.jpeg",
                "*.bmp",
                "*.gif",
                "*.webp",
                "*.tga",
                "*.reanim",
                "*.reanim.compiled",
                "*.xml",
                "*.xml.compiled",
                "*.trail",
                "*.trail.compiled",
                "*.ttf",
                "*.lua"
            ]
        };

        private static FilePickerFileType LanguageFileType => new(Loc.Text("FilePicker.JsonLanguageFiles"))
        {
            Patterns = ["*.json"],
            MimeTypes = ["application/json", "text/json"]
        };

        private static string CreateExportFileName(string projectName)
        {
            string name = string.IsNullOrWhiteSpace(projectName) ? "effectviewer-project" : projectName.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase)
                ? name
                : name + ".zip";
        }
    }
}
