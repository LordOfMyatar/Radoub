using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.ViewModels;

namespace DialogEditor.Views
{
    /// <summary>
    /// Reusable flowchart panel that can be embedded in MainWindow or FlowchartWindow.
    /// Handles graph rendering, zoom controls, and node click events.
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
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                switch (e.Key)
                {
                    case Key.Add:
                    case Key.OemPlus:
                        SetZoom(_currentZoom + ZoomStep);
                        e.Handled = true;
                        break;
                    case Key.Subtract:
                    case Key.OemMinus:
                        SetZoom(_currentZoom - ZoomStep);
                        e.Handled = true;
                        break;
                    case Key.D0:
                    case Key.NumPad0:
                        SetZoom(1.0);
                        e.Handled = true;
                        break;
                }
            }
        }

        private void SetZoom(double zoom)
        {
            _currentZoom = Math.Clamp(zoom, MinZoom, MaxZoom);

            // Apply scale transform via LayoutTransformControl (updates scrollbar extents)
            ZoomContainer.LayoutTransform = new ScaleTransform(_currentZoom, _currentZoom);

            // Update zoom level display
            ZoomLevelText.Text = $"{(int)(_currentZoom * 100)}%";

            UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart zoom: {_currentZoom:P0}");
        }

        private void FitToWindow()
        {
            // Reset to 1.0 first to get accurate unscaled bounds
            ZoomContainer.LayoutTransform = new ScaleTransform(1.0, 1.0);

            // Force layout update to get accurate measurements
            ZoomContainer.UpdateLayout();

            // Get the actual size of the graph panel content (unscaled)
            var graphBounds = FlowchartGraphPanel.Bounds;
            var scrollViewerBounds = FlowchartScrollViewer.Bounds;

            if (graphBounds.Width <= 0 || graphBounds.Height <= 0 ||
                scrollViewerBounds.Width <= 0 || scrollViewerBounds.Height <= 0)
            {
                SetZoom(1.0);
                return;
            }

            // Calculate zoom to fit both width and height
            var scaleX = scrollViewerBounds.Width / graphBounds.Width;
            var scaleY = scrollViewerBounds.Height / graphBounds.Height;
            var fitZoom = Math.Min(scaleX, scaleY) * 0.9; // 90% to leave some margin

            SetZoom(fitZoom);
        }

        #endregion

        #region Mouse Panning

        private void OnScrollViewerPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(FlowchartScrollViewer);

            // Start panning with middle button, or Shift+left button
            bool shouldPan = point.Properties.IsMiddleButtonPressed ||
                            (point.Properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift));

            if (shouldPan)
            {
                _isPanning = true;
                _panStartPoint = point.Position;
                _panStartOffset = new Vector(FlowchartScrollViewer.Offset.X, FlowchartScrollViewer.Offset.Y);

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

            // Update scroll offset (inverted for natural panning feel)
            FlowchartScrollViewer.Offset = new Vector(
                _panStartOffset.X + delta.X,
                _panStartOffset.Y + delta.Y
            );

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
