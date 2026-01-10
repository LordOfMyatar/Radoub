using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using DialogEditor.ViewModels;

namespace DialogEditor.Views
{
    /// <summary>
    /// Reusable flowchart panel that can be embedded in MainWindow or FlowchartWindow.
    /// Handles graph rendering, zoom controls, and node click events.
    /// #809: Now forwards keyboard shortcuts to parent window for feature parity with TreeView.
    /// </summary>
    public partial class FlowchartPanel : UserControl
    {
        private FlowchartPanelViewModel _viewModel;
        private double _currentZoom = 1.0;
        private const double ZoomStep = 0.1;
        private const double MinZoom = 0.25;
        private const double MaxZoom = 3.0;

        // Mouse panning state
        private bool _isPanning;
        private Point _panStartPoint;
        private Vector _panStartOffset;

        // #809: Keyboard shortcut handler for forwarding to parent window
        private KeyboardShortcutManager? _shortcutManager;

        /// <summary>
        /// Sets the keyboard shortcut manager for forwarding shortcuts to the parent window.
        /// #809: Enables keyboard parity with TreeView.
        /// </summary>
        public KeyboardShortcutManager? ShortcutManager
        {
            get => _shortcutManager;
            set => _shortcutManager = value;
        }

        /// <summary>
        /// Raised when a flowchart node is clicked.
        /// The FlowchartNode parameter contains the clicked node with context (IsLink, OriginalPointer, etc.)
        /// </summary>
        public event EventHandler<FlowchartNode?>? NodeClicked;

        /// <summary>
        /// Gets the ViewModel for external access (e.g., selection sync)
        /// </summary>
        public FlowchartPanelViewModel ViewModel => _viewModel;

        public FlowchartPanel()
        {
            InitializeComponent();
            _viewModel = new FlowchartPanelViewModel();
            DataContext = _viewModel;

            // Hook up pointer pressed to handle node clicks
            FlowchartGraphPanel.AddHandler(PointerPressedEvent, OnGraphPanelPointerPressed, RoutingStrategies.Tunnel);

            // Hook up mouse wheel for zoom
            FlowchartScrollViewer.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);

            // Hook up mouse panning (middle button or Shift+left button)
            FlowchartScrollViewer.AddHandler(PointerPressedEvent, OnScrollViewerPointerPressed, RoutingStrategies.Tunnel);
            FlowchartScrollViewer.AddHandler(PointerMovedEvent, OnScrollViewerPointerMoved, RoutingStrategies.Tunnel);
            FlowchartScrollViewer.AddHandler(PointerReleasedEvent, OnScrollViewerPointerReleased, RoutingStrategies.Tunnel);

            // Keyboard shortcuts when panel has focus
            KeyDown += OnKeyDown;

            // Listen for settings changes to refresh colors (#340)
            SettingsService.Instance.PropertyChanged += OnSettingsChanged;

            // Listen for theme changes to refresh colors (#141)
            ThemeManager.Instance.ThemeApplied += OnThemeApplied;

            // Subscribe to collapse/expand events from TreeView (#451)
            DialogChangeEventBus.Instance.DialogChanged += OnDialogChanged;

            // Re-fit when viewport size changes (if in fit mode)
            FlowchartScrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
        }

        private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            // Re-fit when bounds change while in fit mode
            if (_isFitMode && e.Property == BoundsProperty)
            {
                // Debounce to avoid excessive recalculations during resize
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_isFitMode && _viewModel.HasContent)
                    {
                        FitToWindow();
                    }
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
        }

        private void OnDialogChanged(object? sender, DialogChangeEventArgs e)
        {
            // Handle collapse/expand events from TreeView
            if (e.ChangeType == DialogChangeType.NodeCollapsed ||
                e.ChangeType == DialogChangeType.NodeExpanded ||
                e.ChangeType == DialogChangeType.AllCollapsed ||
                e.ChangeType == DialogChangeType.AllExpanded)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _viewModel.HandleExternalCollapseEvent(e);
                });
            }
        }

        private void OnThemeApplied(object? sender, EventArgs e)
        {
            // Refresh flowchart colors when theme changes
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Force complete visual refresh by toggling visibility
                // This forces Avalonia to recreate the visual tree with new theme colors
                FlowchartScrollViewer.IsVisible = false;
                _viewModel.RefreshGraph();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    FlowchartScrollViewer.IsVisible = true;
                    UnifiedLogger.LogUI(LogLevel.DEBUG, "Flowchart colors refreshed after theme change");
                }, Avalonia.Threading.DispatcherPriority.Background);
            });
        }

        private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Refresh visual when NPC speaker preferences change
            if (e.PropertyName == nameof(SettingsService.NpcSpeakerPreferences) ||
                e.PropertyName == nameof(SettingsService.EnableNpcTagColoring))
            {
                // Ensure we're on the UI thread
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // Force re-render by refreshing the Graph binding
                    // This triggers DataTemplate re-evaluation with updated colors
                    _viewModel.RefreshGraph();
                    UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart colors refreshed due to {e.PropertyName} change");
                });
            }
        }

        #region Zoom Controls

        private void OnZoomInClick(object? sender, RoutedEventArgs e)
        {
            SetZoom(_currentZoom + ZoomStep);
        }

        private void OnZoomOutClick(object? sender, RoutedEventArgs e)
        {
            SetZoom(_currentZoom - ZoomStep);
        }

        private void OnZoomResetClick(object? sender, RoutedEventArgs e)
        {
            SetZoom(1.0);
        }

        private void OnZoomFitClick(object? sender, RoutedEventArgs e)
        {
            FitToWindow();
        }

        private void OnRefreshClick(object? sender, RoutedEventArgs e)
        {
            // Force complete visual refresh by toggling visibility
            // This forces Avalonia to recreate the visual tree and re-evaluate all converters
            FlowchartScrollViewer.IsVisible = false;
            _viewModel.RefreshGraph();

            // Re-show after a short delay to ensure the graph is rebuilt
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                FlowchartScrollViewer.IsVisible = true;
                UnifiedLogger.LogUI(LogLevel.DEBUG, "Flowchart manually refreshed with visual tree rebuild");
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        private void OnCollapseAllClick(object? sender, RoutedEventArgs e)
        {
            _viewModel.CollapseAll();
        }

        private void OnExpandAllClick(object? sender, RoutedEventArgs e)
        {
            _viewModel.ExpandAll();
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            // Only zoom if Ctrl is held
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                var delta = e.Delta.Y > 0 ? ZoomStep : -ZoomStep;
                SetZoom(_currentZoom + delta);
                e.Handled = true;
            }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            // #809: Handle zoom shortcuts locally
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                switch (e.Key)
                {
                    case Key.Add:
                    case Key.OemPlus:
                        SetZoom(_currentZoom + ZoomStep);
                        e.Handled = true;
                        return;
                    case Key.Subtract:
                    case Key.OemMinus:
                        SetZoom(_currentZoom - ZoomStep);
                        e.Handled = true;
                        return;
                    case Key.D0:
                    case Key.NumPad0:
                        SetZoom(1.0);
                        e.Handled = true;
                        return;
                }
            }

            // #809: Forward other shortcuts to parent window's shortcut manager
            if (_shortcutManager != null)
            {
                // Try tunneling shortcuts first (Ctrl+Z/Y for undo/redo, Ctrl+Shift+Up/Down for move)
                if (_shortcutManager.HandlePreviewKeyDown(e))
                {
                    e.Handled = true;
                    return;
                }

                // Then try bubbling shortcuts
                if (_shortcutManager.HandleKeyDown(e))
                {
                    e.Handled = true;
                }
            }
        }

        private void SetZoom(double zoom)
        {
            _currentZoom = Math.Clamp(zoom, MinZoom, MaxZoom);

            // Exit fit mode when using normal zoom controls
            if (_isFitMode)
            {
                FitContainer.Margin = new Thickness(0);
                ZoomContainer.Margin = new Thickness(0);
                FlowchartGraphPanel.Margin = new Thickness(0);
                // Reset to Stretch so ScrollViewer can scroll when content exceeds viewport
                FitContainer.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                FitContainer.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
                _isFitMode = false;
            }

            // Apply scale transform via LayoutTransformControl (updates scrollbar extents)
            ZoomContainer.LayoutTransform = new ScaleTransform(_currentZoom, _currentZoom);

            // Update zoom level display
            ZoomLevelText.Text = $"{(int)(_currentZoom * 100)}%";

            // Log extent vs viewport for debugging scrollbar/panning behavior
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var maxScrollX = FlowchartScrollViewer.Extent.Width - FlowchartScrollViewer.Viewport.Width;
                var maxScrollY = FlowchartScrollViewer.Extent.Height - FlowchartScrollViewer.Viewport.Height;
                UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart zoom: {_currentZoom:P0}, extent=({FlowchartScrollViewer.Extent.Width:F0}x{FlowchartScrollViewer.Extent.Height:F0}), viewport=({FlowchartScrollViewer.Viewport.Width:F0}x{FlowchartScrollViewer.Viewport.Height:F0}), maxScroll=({maxScrollX:F0},{maxScrollY:F0})");
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        // Track if we're in "fit mode" (with centered alignment)
        private bool _isFitMode;

        private void FitToWindow()
        {
            // Reset to 1.0 first to get accurate unscaled bounds
            ZoomContainer.LayoutTransform = new ScaleTransform(1.0, 1.0);
            ZoomContainer.Margin = new Thickness(0);
            ZoomContainer.UpdateLayout();

            var scrollViewerBounds = FlowchartScrollViewer.Bounds;
            if (scrollViewerBounds.Width <= 0 || scrollViewerBounds.Height <= 0)
            {
                SetZoom(1.0);
                return;
            }

            // Find the actual content bounds by scanning visual children
            var contentBounds = GetGraphContentBounds();
            if (contentBounds == null)
            {
                SetZoom(1.0);
                return;
            }

            var (_, _, contentWidth, contentHeight) = contentBounds.Value;

            // Calculate zoom based on actual content size (not panel size)
            var scaleX = scrollViewerBounds.Width / contentWidth;
            var scaleY = scrollViewerBounds.Height / contentHeight;
            var fitZoom = Math.Min(scaleX, scaleY) * 0.9; // 90% to leave margin

            _isFitMode = true;

            // Apply zoom
            _currentZoom = Math.Clamp(fitZoom, MinZoom, MaxZoom);
            ZoomContainer.LayoutTransform = new ScaleTransform(_currentZoom, _currentZoom);
            ZoomLevelText.Text = $"{(int)(_currentZoom * 100)}%";

            // Set Center alignment for fit mode centering
            FitContainer.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            FitContainer.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

            // Reset margins and scroll
            FlowchartGraphPanel.Margin = new Thickness(0);
            ZoomContainer.Margin = new Thickness(0);
            FitContainer.Margin = new Thickness(0);
            FlowchartScrollViewer.Offset = new Vector(0, 0);

            UnifiedLogger.LogUI(LogLevel.DEBUG, $"FitToWindow: zoom={_currentZoom:P0}, content=({contentWidth:F0}x{contentHeight:F0})");
        }

        /// <summary>
        /// Gets the actual bounding box of graph content (nodes and edges), excluding dead space.
        /// Returns null if no content found.
        /// </summary>
        private (double minX, double minY, double width, double height)? GetGraphContentBounds()
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var child in FlowchartGraphPanel.GetVisualChildren())
            {
                if (child is Visual visual && visual.Bounds.Width > 0 && visual.Bounds.Height > 0)
                {
                    var bounds = visual.Bounds;
                    minX = Math.Min(minX, bounds.X);
                    minY = Math.Min(minY, bounds.Y);
                    maxX = Math.Max(maxX, bounds.Right);
                    maxY = Math.Max(maxY, bounds.Bottom);
                }
            }

            if (minX == double.MaxValue)
                return null;

            return (minX, minY, maxX - minX, maxY - minY);
        }

        #endregion

        #region Mouse Panning

        private void OnScrollViewerPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(FlowchartScrollViewer);

            // Start panning with middle button, or Shift+left button
            bool shouldPan = point.Properties.IsMiddleButtonPressed ||
                            (point.Properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift));

            UnifiedLogger.LogUI(LogLevel.DEBUG, $"PointerPressed: left={point.Properties.IsLeftButtonPressed}, shift={e.KeyModifiers.HasFlag(KeyModifiers.Shift)}, shouldPan={shouldPan}");

            if (shouldPan)
            {
                _isPanning = true;
                _panStartPoint = point.Position;
                _panStartOffset = new Vector(FlowchartScrollViewer.Offset.X, FlowchartScrollViewer.Offset.Y);

                var maxScrollX = FlowchartScrollViewer.Extent.Width - FlowchartScrollViewer.Viewport.Width;
                var maxScrollY = FlowchartScrollViewer.Extent.Height - FlowchartScrollViewer.Viewport.Height;
                var canPan = maxScrollX > 0 || maxScrollY > 0;
                UnifiedLogger.LogUI(LogLevel.DEBUG, $"Panning started: offset=({_panStartOffset.X:F0},{_panStartOffset.Y:F0}), extent=({FlowchartScrollViewer.Extent.Width:F0}x{FlowchartScrollViewer.Extent.Height:F0}), viewport=({FlowchartScrollViewer.Viewport.Width:F0}x{FlowchartScrollViewer.Viewport.Height:F0}), maxScroll=({maxScrollX:F0},{maxScrollY:F0}), canPan={canPan}");

                // Capture the pointer for reliable tracking
                e.Pointer.Capture(FlowchartScrollViewer);
                e.Handled = true;

                // Change cursor to indicate panning
                FlowchartScrollViewer.Cursor = new Avalonia.Input.Cursor(StandardCursorType.SizeAll);
            }
        }

        private void OnScrollViewerPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPanning) return;

            var currentPoint = e.GetPosition(FlowchartScrollViewer);
            var delta = _panStartPoint - currentPoint;

            // Calculate max scroll offsets
            var maxScrollX = Math.Max(0, FlowchartScrollViewer.Extent.Width - FlowchartScrollViewer.Viewport.Width);
            var maxScrollY = Math.Max(0, FlowchartScrollViewer.Extent.Height - FlowchartScrollViewer.Viewport.Height);

            // Update scroll offset (clamped to valid range)
            var newOffsetX = Math.Clamp(_panStartOffset.X + delta.X, 0, maxScrollX);
            var newOffsetY = Math.Clamp(_panStartOffset.Y + delta.Y, 0, maxScrollY);
            FlowchartScrollViewer.Offset = new Vector(newOffsetX, newOffsetY);

            e.Handled = true;
        }

        private void OnScrollViewerPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                e.Pointer.Capture(null);
                FlowchartScrollViewer.Cursor = null; // Reset cursor

                e.Handled = true;
            }
        }

        #endregion

        #region Node Click Handling

        private void OnGraphPanelPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(FlowchartGraphPanel);

            // Check for panning first - Shift+left or middle button starts pan mode
            bool shouldPan = point.Properties.IsMiddleButtonPressed ||
                            (point.Properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift));

            if (shouldPan)
            {
                // Delegate to ScrollViewer panning
                _isPanning = true;
                _panStartPoint = e.GetPosition(FlowchartScrollViewer);
                _panStartOffset = new Vector(FlowchartScrollViewer.Offset.X, FlowchartScrollViewer.Offset.Y);

                var maxScrollX = FlowchartScrollViewer.Extent.Width - FlowchartScrollViewer.Viewport.Width;
                var maxScrollY = FlowchartScrollViewer.Extent.Height - FlowchartScrollViewer.Viewport.Height;
                var canPan = maxScrollX > 0 || maxScrollY > 0;
                UnifiedLogger.LogUI(LogLevel.DEBUG, $"Panning started (from GraphPanel): offset=({_panStartOffset.X:F0},{_panStartOffset.Y:F0}), extent=({FlowchartScrollViewer.Extent.Width:F0}x{FlowchartScrollViewer.Extent.Height:F0}), viewport=({FlowchartScrollViewer.Viewport.Width:F0}x{FlowchartScrollViewer.Viewport.Height:F0}), maxScroll=({maxScrollX:F0},{maxScrollY:F0}), canPan={canPan}");

                e.Pointer.Capture(FlowchartScrollViewer);
                e.Handled = true;
                FlowchartScrollViewer.Cursor = new Avalonia.Input.Cursor(StandardCursorType.SizeAll);
                return;
            }

            // Find if we clicked on a FlowchartNode
            var source = e.Source as Visual;
            if (source == null) return;

            // Walk up the visual tree to find the Border with FlowchartNode DataContext
            FlowchartNode? clickedNode = null;
            var current = source;
            while (current != null)
            {
                if (current is Border border && border.DataContext is FlowchartNode node)
                {
                    clickedNode = node;
                    break;
                }
                current = current.GetVisualParent();
            }

            if (clickedNode != null)
            {
                var clickCount = e.ClickCount;

                // Double-click toggles collapse (if node has children) (#251)
                if (clickCount == 2 && clickedNode.ChildCount > 0)
                {
                    UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart node double-clicked: toggling collapse for {clickedNode.Id}");
                    _viewModel.ToggleCollapse(clickedNode.Id);
                    e.Handled = true;
                    return;
                }

                UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart node clicked: {clickedNode.Id} - {clickedNode.ShortText} (IsLink: {clickedNode.IsLink})");

                // Directly set selection to the clicked node's ID (not by DialogNode lookup)
                // This ensures link nodes get selected, not their targets
                _viewModel.SelectedNodeId = clickedNode.Id;

                // Raise the event with the FlowchartNode (includes IsLink, OriginalPointer context)
                NodeClicked?.Invoke(this, clickedNode);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Update the flowchart to display the given dialog
        /// </summary>
        /// <param name="dialog">The dialog to display</param>
        /// <param name="fileName">Optional filename for display</param>
        public void UpdateDialog(Dialog? dialog, string? fileName = null)
        {
            _viewModel.UpdateDialog(dialog, fileName);
        }

        /// <summary>
        /// Clear the flowchart display
        /// </summary>
        public void Clear()
        {
            _viewModel.Clear();
        }

        /// <summary>
        /// Select a node in the flowchart by DialogNode.
        /// Used for TreeView → Flowchart sync.
        /// </summary>
        public void SelectNode(DialogNode? dialogNode)
        {
            _viewModel.SelectNode(dialogNode);
        }

        /// <summary>
        /// Gets or sets the selected node ID directly
        /// </summary>
        public string? SelectedNodeId
        {
            get => _viewModel.SelectedNodeId;
            set => _viewModel.SelectedNodeId = value;
        }

        #endregion

        #region Export

        /// <summary>
        /// Export the flowchart to PNG format
        /// </summary>
        /// <param name="filePath">Destination file path</param>
        /// <param name="dpi">Resolution (default 96 DPI)</param>
        /// <returns>True if export succeeded</returns>
        public async Task<bool> ExportToPngAsync(string filePath, double dpi = 96)
        {
            return await FlowchartExportService.ExportToPngAsync(FlowchartGraphPanel, filePath, dpi);
        }

        /// <summary>
        /// Export the flowchart to SVG format
        /// </summary>
        /// <param name="filePath">Destination file path</param>
        /// <returns>True if export succeeded</returns>
        public async Task<bool> ExportToSvgAsync(string filePath)
        {
            var graph = _viewModel.FlowchartGraph;
            if (graph == null || graph.IsEmpty)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot export: no flowchart content");
                return false;
            }

            return await FlowchartExportService.ExportToSvgAsync(graph, filePath, _viewModel.FileName);
        }

        #endregion
    }
}
