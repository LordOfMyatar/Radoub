using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using DialogEditor.Models;
using DialogEditor.Services;
using Microsoft.Extensions.DependencyInjection;
using Radoub.Formats.Logging;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using DialogEditor.ViewModels;

namespace DialogEditor.Views
{
    /// <summary>
    /// Event args for context menu actions in FlowchartPanel (#461).
    /// </summary>
    public class FlowchartContextMenuEventArgs : EventArgs
    {
        public string Action { get; }
        public FlowchartNode Node { get; }

        public FlowchartContextMenuEventArgs(string action, FlowchartNode node)
        {
            Action = action;
            Node = node;
        }
    }

    /// <summary>
    /// Reusable flowchart panel that can be embedded in MainWindow or FlowchartWindow.
    /// Handles graph rendering, zoom controls, and node click events.
    /// #809: Now forwards keyboard shortcuts to parent window for feature parity with TreeView.
    /// </summary>
    public partial class FlowchartPanel : UserControl
    {
        private readonly ISettingsService _settings;
        private readonly UISettingsService _uiSettings;
        private FlowchartPanelViewModel _viewModel;
        private double _currentZoom = 1.0;
        private const double ZoomStep = 0.1;
        private const double MinZoom = 0.25;
        private const double MaxZoom = 3.0;

        // Mouse panning state
        private bool _isPanning;
        private Point _panStartPoint;
        private Vector _panStartOffset;

        // Drag-drop sibling reorder state (#240)
        private const double DragThreshold = 5.0;
        private bool _isDragging;
        private bool _dragPotential; // pointer pressed on node, waiting for threshold
        private Point _dragStartPoint;
        private FlowchartNode? _draggedNode;

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
        /// Raised when a context menu action is requested (#461).
        /// The string parameter is the action name (e.g., "AddNode", "DeleteNode").
        /// </summary>
        public event EventHandler<FlowchartContextMenuEventArgs>? ContextMenuAction;

        /// <summary>
        /// Raised when a drag-drop sibling reorder is requested (#240).
        /// Args: (DialogNode node, DialogNode? parent, int fromIndex, int toIndex)
        /// </summary>
        public event Action<DialogNode, DialogNode?, int, int>? SiblingReorderRequested;

        /// <summary>
        /// Raised when a reparent is requested via drag-drop (#1965).
        /// Args: (DialogNode node, DialogPtr? sourcePointer, DialogNode? newParent, int insertIndex)
        /// </summary>
        public event Action<DialogNode, DialogPtr?, DialogNode?, int>? ReparentRequested;

        /// <summary>
        /// Gets the ViewModel for external access (e.g., selection sync)
        /// </summary>
        public FlowchartPanelViewModel ViewModel => _viewModel;

        public FlowchartPanel()
        {
            _settings = Program.Services.GetRequiredService<ISettingsService>();
            _uiSettings = Program.Services.GetRequiredService<UISettingsService>();
            InitializeComponent();
            _viewModel = new FlowchartPanelViewModel();
            DataContext = _viewModel;

            // Hook up pointer events for node clicks and drag-drop (#240)
            FlowchartGraphPanel.AddHandler(PointerPressedEvent, OnGraphPanelPointerPressed, RoutingStrategies.Tunnel);
            FlowchartGraphPanel.AddHandler(PointerMovedEvent, OnGraphPanelPointerMoved, RoutingStrategies.Tunnel);
            FlowchartGraphPanel.AddHandler(PointerReleasedEvent, OnGraphPanelPointerReleased, RoutingStrategies.Tunnel);

            // Hook up mouse wheel for zoom
            FlowchartScrollViewer.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);

            // Hook up mouse panning (middle button or Shift+left button)
            FlowchartScrollViewer.AddHandler(PointerPressedEvent, OnScrollViewerPointerPressed, RoutingStrategies.Tunnel);
            FlowchartScrollViewer.AddHandler(PointerMovedEvent, OnScrollViewerPointerMoved, RoutingStrategies.Tunnel);
            FlowchartScrollViewer.AddHandler(PointerReleasedEvent, OnScrollViewerPointerReleased, RoutingStrategies.Tunnel);

            // Keyboard shortcuts when panel has focus
            KeyDown += OnKeyDown;

            // Listen for settings changes to refresh colors (#340)
            _settings.PropertyChanged += OnSettingsChanged;

            // Listen for UI settings changes (#813: FlowchartNodeMaxLines)
            _uiSettings.PropertyChanged += OnUISettingsChanged;

            // Listen for theme changes to refresh colors (#141)
            ThemeManager.Instance.ThemeApplied += OnThemeApplied;

            // Subscribe to collapse/expand events from TreeView (#451)
            DialogChangeEventBus.Instance.DialogChanged += OnDialogChanged;

            // Re-fit when viewport size changes (if in fit mode)
            FlowchartScrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;

            // Context menu click handlers are attached via XAML Click events (#461)

            // Unsubscribe from singleton events when control is detached (#1282)
            DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            ThemeManager.Instance.ThemeApplied -= OnThemeApplied;
            DialogChangeEventBus.Instance.DialogChanged -= OnDialogChanged;
        }

        // Track the current node for context menu actions
        private FlowchartNode? _contextMenuNode;

        /// <summary>
        /// Called when context menu opens - captures the FlowchartNode from the Border's DataContext.
        /// </summary>
        private void OnContextMenuOpened(object? sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu)
            {
                // The ContextMenu's DataContext should be bound to the FlowchartNode
                _contextMenuNode = contextMenu.DataContext as FlowchartNode;
                if (_contextMenuNode != null)
                {
                    UnifiedLogger.LogUI(LogLevel.DEBUG, $"Context menu opened for node: {_contextMenuNode.Id}");
                }
                else
                {
                    UnifiedLogger.LogUI(LogLevel.WARN, "Context menu opened but no FlowchartNode in DataContext");
                }
            }
        }

        private void OnContextMenuItemClick(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string action && _contextMenuNode != null)
            {
                UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart context menu: {action} on node {_contextMenuNode.Id}");

                // Ensure the node is selected before the action
                _viewModel.SelectedNodeId = _contextMenuNode.Id;
                NodeClicked?.Invoke(this, _contextMenuNode);

                // Raise the context menu action event
                ContextMenuAction?.Invoke(this, new FlowchartContextMenuEventArgs(action, _contextMenuNode));
            }
            else if (_contextMenuNode == null)
            {
                UnifiedLogger.LogUI(LogLevel.WARN, $"Flowchart context menu: action requested but no node captured");
            }
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

            // #809: Refresh FlowView on structural changes and property edits
            if (e.ChangeType == DialogChangeType.NodeAdded ||
                e.ChangeType == DialogChangeType.NodeDeleted ||
                e.ChangeType == DialogChangeType.NodeMoved ||
                e.ChangeType == DialogChangeType.NodeModified ||
                e.ChangeType == DialogChangeType.DialogRefreshed)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _viewModel.RefreshGraph();
                    UnifiedLogger.LogUI(LogLevel.DEBUG, $"FlowView refreshed due to {e.ChangeType}");
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
                // Force complete visual refresh by toggling visibility (#1223)
                // Same pattern as OnThemeApplied - Avalonia needs visual tree recreation
                // to re-evaluate converters with updated speaker colors
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    FlowchartScrollViewer.IsVisible = false;
                    _viewModel.RefreshGraph();

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        FlowchartScrollViewer.IsVisible = true;
                        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart colors refreshed due to {e.PropertyName} change");
                    }, Avalonia.Threading.DispatcherPriority.Background);
                });
            }
        }

        private void OnUISettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Refresh when flowchart node max lines changes (#813)
            if (e.PropertyName == nameof(UISettingsService.FlowchartNodeMaxLines))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _viewModel.OnPropertyChanged(nameof(FlowchartPanelViewModel.NodeMaxLines));
                    _viewModel.RefreshGraph();
                    UnifiedLogger.LogUI(LogLevel.DEBUG, "Flowchart refreshed due to NodeMaxLines change");
                });
            }

            // Refresh when flowchart node width changes (#906)
            if (e.PropertyName == nameof(UISettingsService.FlowchartNodeWidth))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _viewModel.OnPropertyChanged(nameof(FlowchartPanelViewModel.NodeWidth));
                    _viewModel.OnPropertyChanged(nameof(FlowchartPanelViewModel.NodeMinWidth));
                    _viewModel.OnPropertyChanged(nameof(FlowchartPanelViewModel.NodeTextMaxWidth));
                    _viewModel.RefreshGraph();
                    UnifiedLogger.LogUI(LogLevel.DEBUG, "Flowchart refreshed due to NodeWidth change");
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

                UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart node clicked: {clickedNode.Id} - {clickedNode.DisplayText} (IsLink: {clickedNode.IsLink})");

                // Directly set selection to the clicked node's ID (not by DialogNode lookup)
                // This ensures link nodes get selected, not their targets
                _viewModel.SelectedNodeId = clickedNode.Id;

                // Raise the event with the FlowchartNode (includes IsLink, OriginalPointer context)
                NodeClicked?.Invoke(this, clickedNode);

                // #240/#1965: Start tracking potential drag (left-button single-click, including link nodes)
                if (clickCount == 1 && point.Properties.IsLeftButtonPressed)
                {
                    _dragPotential = true;
                    _dragStartPoint = e.GetPosition(FlowchartGraphPanel);
                    _draggedNode = clickedNode;
                }
            }
        }

        /// <summary>
        /// Handles pointer move during potential drag (#240).
        /// Detects drag threshold and shows insertion indicator for sibling reorder.
        /// </summary>
        private void OnGraphPanelPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_dragPotential && !_isDragging) return;
            if (_isPanning) return;

            var currentPos = e.GetPosition(FlowchartGraphPanel);

            // Check threshold to start drag
            if (_dragPotential && !_isDragging)
            {
                var delta = currentPos - _dragStartPoint;
                if (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold)
                {
                    _isDragging = true;
                    _dragPotential = false;
                    FlowchartGraphPanel.Cursor = new Avalonia.Input.Cursor(StandardCursorType.DragMove);
                    UnifiedLogger.LogUI(LogLevel.DEBUG, $"Drag started for node {_draggedNode?.Id}");
                }
                else
                {
                    return; // Not yet past threshold
                }
            }

            if (!_isDragging || _draggedNode == null) return;

            // Find target node under cursor
            var targetNode = FindFlowchartNodeAtPoint(currentPos);
            if (targetNode != null && targetNode != _draggedNode)
            {
                if (AreSiblings(targetNode, _draggedNode))
                {
                    // Sibling reorder (#240)
                    ShowInsertionIndicator(targetNode, currentPos);
                    FlowchartGraphPanel.Cursor = new Avalonia.Input.Cursor(StandardCursorType.DragMove);
                }
                else if (IsValidReparentTarget(targetNode, _draggedNode))
                {
                    // Valid reparent target (#1965)
                    ShowInsertionIndicator(targetNode, currentPos);
                    FlowchartGraphPanel.Cursor = new Avalonia.Input.Cursor(StandardCursorType.DragLink);
                }
                else
                {
                    // Invalid target
                    HideInsertionIndicator();
                    FlowchartGraphPanel.Cursor = new Avalonia.Input.Cursor(StandardCursorType.No);
                }
            }
            else
            {
                HideInsertionIndicator();
                FlowchartGraphPanel.Cursor = new Avalonia.Input.Cursor(StandardCursorType.DragMove);
            }
        }

        /// <summary>
        /// Handles pointer release to execute or cancel drag (#240).
        /// </summary>
        private void OnGraphPanelPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDragging && _draggedNode != null)
            {
                var currentPos = e.GetPosition(FlowchartGraphPanel);
                var targetNode = FindFlowchartNodeAtPoint(currentPos);

                if (targetNode != null && targetNode != _draggedNode)
                {
                    if (AreSiblings(targetNode, _draggedNode))
                    {
                        ExecuteSiblingReorder(_draggedNode, targetNode, currentPos);
                    }
                    else if (IsValidReparentTarget(targetNode, _draggedNode))
                    {
                        ExecuteReparent(_draggedNode, targetNode);
                    }
                }

                HideInsertionIndicator();
                FlowchartGraphPanel.Cursor = Avalonia.Input.Cursor.Default;
                UnifiedLogger.LogUI(LogLevel.DEBUG, "Drag ended");
            }

            ResetDragState();
        }

        private void ResetDragState()
        {
            _isDragging = false;
            _dragPotential = false;
            _draggedNode = null;
        }

        /// <summary>
        /// Finds the FlowchartNode at a given point by walking the visual tree.
        /// </summary>
        private FlowchartNode? FindFlowchartNodeAtPoint(Point point)
        {
            var hit = FlowchartGraphPanel.InputHitTest(point);
            if (hit is not Visual visual) return null;

            var current = visual;
            while (current != null)
            {
                if (current is Border border && border.DataContext is FlowchartNode node)
                    return node;
                current = current.GetVisualParent();
            }
            return null;
        }

        /// <summary>
        /// Checks if two FlowchartNodes are siblings (share the same parent in the dialog structure).
        /// </summary>
        private bool AreSiblings(FlowchartNode a, FlowchartNode b)
        {
            if (a.OriginalNode == null || b.OriginalNode == null) return false;

            var graph = _viewModel.FlowchartGraph;
            if (graph == null) return false;

            // Check if both are root-level (in Dialog.Starts)
            var dialog = _viewModel.CurrentDialog;
            if (dialog == null) return false;

            bool aIsRoot = dialog.Starts.Any(s => s.Node == a.OriginalNode);
            bool bIsRoot = dialog.Starts.Any(s => s.Node == b.OriginalNode);
            if (aIsRoot && bIsRoot) return true;

            // Check if they share a parent
            foreach (var entry in dialog.Entries)
            {
                bool hasA = entry.Pointers.Any(p => p.Node == a.OriginalNode);
                bool hasB = entry.Pointers.Any(p => p.Node == b.OriginalNode);
                if (hasA && hasB) return true;
            }
            foreach (var reply in dialog.Replies)
            {
                bool hasA = reply.Pointers.Any(p => p.Node == a.OriginalNode);
                bool hasB = reply.Pointers.Any(p => p.Node == b.OriginalNode);
                if (hasA && hasB) return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves sibling indices and raises SiblingReorderRequested event (#240).
        /// MainWindow handles undo state + actual reorder execution.
        /// </summary>
        private void ExecuteSiblingReorder(FlowchartNode draggedNode, FlowchartNode targetNode, Point dropPoint)
        {
            if (draggedNode.OriginalNode == null || targetNode.OriginalNode == null) return;

            var dialog = _viewModel.CurrentDialog;
            if (dialog == null) return;

            // Find shared parent and indices
            DialogNode? parent = null;
            int fromIndex = -1;
            int toIndex = -1;

            // Check root level
            bool draggedIsRoot = false;
            for (int i = 0; i < dialog.Starts.Count; i++)
            {
                if (dialog.Starts[i].Node == draggedNode.OriginalNode) { fromIndex = i; draggedIsRoot = true; }
                if (dialog.Starts[i].Node == targetNode.OriginalNode) toIndex = i;
            }

            if (!draggedIsRoot)
            {
                // Find parent node
                foreach (var entry in dialog.Entries.Concat(dialog.Replies))
                {
                    fromIndex = -1;
                    toIndex = -1;
                    for (int i = 0; i < entry.Pointers.Count; i++)
                    {
                        if (entry.Pointers[i].Node == draggedNode.OriginalNode) fromIndex = i;
                        if (entry.Pointers[i].Node == targetNode.OriginalNode) toIndex = i;
                    }
                    if (fromIndex >= 0 && toIndex >= 0)
                    {
                        parent = entry;
                        break;
                    }
                }
            }

            if (fromIndex < 0 || toIndex < 0) return;

            // Raise event for MainWindow to handle undo + execution
            SiblingReorderRequested?.Invoke(draggedNode.OriginalNode, parent, fromIndex, toIndex);
        }

        /// <summary>
        /// Checks if a target node is a valid reparent destination for the dragged node (#1965).
        /// Enforces Entry↔Reply alternation and prevents circular references.
        /// </summary>
        private bool IsValidReparentTarget(FlowchartNode target, FlowchartNode dragged)
        {
            if (target.OriginalNode == null || dragged.OriginalNode == null) return false;

            var dialog = _viewModel.CurrentDialog;
            if (dialog == null) return false;

            // Alternation rule: Entry nodes can only be children of Reply nodes (or root)
            // Reply nodes can only be children of Entry nodes
            var draggedType = dragged.OriginalNode.Type;
            var targetType = target.OriginalNode.Type;

            // Drop INTO target as new child — dragged becomes child of target
            // Entry dragged → target must be Reply (or drop to root, handled separately)
            // Reply dragged → target must be Entry
            if (draggedType == DialogNodeType.Entry && targetType != DialogNodeType.Reply) return false;
            if (draggedType == DialogNodeType.Reply && targetType != DialogNodeType.Entry) return false;

            // Prevent circular reference: target cannot be a descendant of dragged
            if (IsDescendantOf(target.OriginalNode, dragged.OriginalNode, dialog)) return false;

            return true;
        }

        /// <summary>
        /// Checks if possibleDescendant is reachable from possibleAncestor (#1965).
        /// </summary>
        private bool IsDescendantOf(DialogNode possibleDescendant, DialogNode possibleAncestor, Dialog dialog)
        {
            var visited = new HashSet<DialogNode>();
            return IsReachableFrom(possibleAncestor, possibleDescendant, visited);
        }

        private bool IsReachableFrom(DialogNode current, DialogNode target, HashSet<DialogNode> visited)
        {
            if (current == target) return true;
            if (visited.Contains(current)) return false;
            visited.Add(current);

            foreach (var ptr in current.Pointers)
            {
                if (ptr.Node != null && IsReachableFrom(ptr.Node, target, visited))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Executes reparent via the ReparentRequested event (#1965).
        /// MainWindow handles undo state + MoveNodeToPosition execution.
        /// </summary>
        private void ExecuteReparent(FlowchartNode draggedNode, FlowchartNode targetNode)
        {
            if (draggedNode.OriginalNode == null || targetNode.OriginalNode == null) return;

            var dialog = _viewModel.CurrentDialog;
            if (dialog == null) return;

            // Use OriginalPointer if available (e.g., link nodes), otherwise search
            DialogPtr? sourcePointer = draggedNode.OriginalPointer ?? FindSourcePointer(dialog, draggedNode.OriginalNode);

            // Insert at end of target's children
            int insertIndex = targetNode.OriginalNode.Pointers.Count;

            ReparentRequested?.Invoke(draggedNode.OriginalNode, sourcePointer, targetNode.OriginalNode, insertIndex);
        }

        /// <summary>
        /// Finds the DialogPtr that owns a node (for sourcePointer parameter in MoveNodeToPosition).
        /// </summary>
        private DialogPtr? FindSourcePointer(Dialog dialog, DialogNode node)
        {
            // Check root level
            var rootPtr = dialog.Starts.FirstOrDefault(s => s.Node == node);
            if (rootPtr != null) return rootPtr;

            // Check all parents
            foreach (var entry in dialog.Entries.Concat(dialog.Replies))
            {
                var ptr = entry.Pointers.FirstOrDefault(p => p.Node == node && !p.IsLink);
                if (ptr != null) return ptr;
            }

            // Fallback: check link pointers
            foreach (var entry in dialog.Entries.Concat(dialog.Replies))
            {
                var ptr = entry.Pointers.FirstOrDefault(p => p.Node == node);
                if (ptr != null) return ptr;
            }

            return null;
        }

        /// <summary>
        /// Shows an insertion indicator line near the target node (#240).
        /// </summary>
        private void ShowInsertionIndicator(FlowchartNode targetNode, Point cursorPos)
        {
            // For now, change the cursor to indicate valid drop
            // Full visual indicator requires overlay on the GraphPanel which is complex
            // with AvaloniaGraphControl's layout system — the cursor change provides feedback
            FlowchartGraphPanel.Cursor = new Avalonia.Input.Cursor(StandardCursorType.DragMove);
        }

        /// <summary>
        /// Hides the insertion indicator (#240).
        /// </summary>
        private void HideInsertionIndicator()
        {
            // Clear any visual indicator state
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



        #endregion
    }
}
