using System;
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
    /// Code-behind for the FlowchartWindow.
    /// Hosts the native flowchart visualization for dialog structure.
    /// </summary>
    public partial class FlowchartWindow : Window
    {
        private readonly FlowchartPanelViewModel _viewModel;
        private double _currentZoom = 1.0;
        private const double ZoomStep = 0.1;
        private const double MinZoom = 0.25;
        private const double MaxZoom = 3.0;

        /// <summary>
        /// Raised when a flowchart node is clicked.
        /// The FlowchartNode parameter contains the clicked node with context (IsLink, OriginalPointer, etc.)
        /// </summary>
        public event EventHandler<FlowchartNode?>? NodeClicked;

        public FlowchartWindow()
        {
            InitializeComponent();
            _viewModel = new FlowchartPanelViewModel();
            DataContext = _viewModel;

            // Hook up pointer pressed to handle node clicks
            FlowchartGraphPanel.AddHandler(PointerPressedEvent, OnGraphPanelPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);

            // Hook up mouse wheel for zoom
            FlowchartScrollViewer.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);

            // Keyboard shortcuts
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

            // Apply scale transform to the graph panel
            FlowchartGraphPanel.RenderTransform = new ScaleTransform(_currentZoom, _currentZoom);

            // Update zoom level display
            ZoomLevelText.Text = $"{(int)(_currentZoom * 100)}%";

            UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart zoom: {_currentZoom:P0}");
        }

        private void FitToWindow()
        {
            // Get the actual size of the graph panel content
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

        private void OnGraphPanelPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Find if we clicked on a FlowchartNode
            var source = e.Source as Avalonia.Visual;
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

                // Raise the event with the FlowchartNode (includes IsLink, OriginalPointer context)
                NodeClicked?.Invoke(this, clickedNode);
            }
        }

        /// <summary>
        /// Update the flowchart to display the given dialog
        /// </summary>
        /// <param name="dialog">The dialog to display</param>
        /// <param name="fileName">Optional filename for display</param>
        public void UpdateDialog(Dialog? dialog, string? fileName = null)
        {
            _viewModel.UpdateDialog(dialog, fileName);

            // Update window title with filename
            if (!string.IsNullOrEmpty(fileName))
            {
                Title = $"Flowchart - {System.IO.Path.GetFileName(fileName)}";
            }
            else
            {
                Title = "Flowchart View";
            }
        }

        /// <summary>
        /// Clear the flowchart display
        /// </summary>
        public void Clear()
        {
            _viewModel.Clear();
            Title = "Flowchart View";
        }

        /// <summary>
        /// Select a node in the flowchart by DialogNode.
        /// Used for TreeView → Flowchart sync.
        /// </summary>
        public void SelectNode(DialogNode? dialogNode)
        {
            _viewModel.SelectNode(dialogNode);
        }
    }
}
