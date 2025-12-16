using System;
using Avalonia;
using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Services;

namespace DialogEditor.Views
{
    /// <summary>
    /// Window wrapper for FlowchartPanel.
    /// Used for "Floating Window" layout mode.
    /// </summary>
    public partial class FlowchartWindow : Window
    {
        /// <summary>
        /// Raised when a flowchart node is clicked.
        /// The FlowchartNode parameter contains the clicked node with context (IsLink, OriginalPointer, etc.)
        /// </summary>
        public event EventHandler<FlowchartNode?>? NodeClicked;

        public FlowchartWindow()
        {
            InitializeComponent();

            // Forward node click events from the panel
            FlowchartPanelControl.NodeClicked += (sender, node) => NodeClicked?.Invoke(this, node);

            // Restore window position from settings (#377)
            RestoreWindowPosition();

            // Save position when window moves or resizes
            PositionChanged += OnPositionChanged;
            PropertyChanged += OnPropertyChanged;
            Closed += OnWindowClosed;
        }

        /// <summary>
        /// Restore window position and size from settings (#377)
        /// </summary>
        private void RestoreWindowPosition()
        {
            var settings = SettingsService.Instance;

            // Restore size
            Width = settings.FlowchartWindowWidth;
            Height = settings.FlowchartWindowHeight;

            // Restore position
            if (settings.FlowchartWindowLeft >= 0 && settings.FlowchartWindowTop >= 0)
            {
                var targetPos = new PixelPoint((int)settings.FlowchartWindowLeft, (int)settings.FlowchartWindowTop);

                // Validate position is on a visible screen
                if (IsPositionOnScreen(targetPos))
                {
                    Position = targetPos;
                }
            }
        }

        /// <summary>
        /// Check if position is visible on any screen
        /// </summary>
        private bool IsPositionOnScreen(PixelPoint position)
        {
            var screens = Screens.All;
            foreach (var screen in screens)
            {
                var bounds = screen.Bounds;
                if (position.X >= bounds.X - 50 &&
                    position.X < bounds.X + bounds.Width &&
                    position.Y >= bounds.Y - 50 &&
                    position.Y < bounds.Y + bounds.Height)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnPositionChanged(object? sender, PixelPointEventArgs e)
        {
            SaveWindowPosition();
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == WidthProperty || e.Property == HeightProperty)
            {
                SaveWindowPosition();
            }
        }

        private void SaveWindowPosition()
        {
            var settings = SettingsService.Instance;
            if (Position.X >= 0 && Position.Y >= 0)
            {
                settings.FlowchartWindowLeft = Position.X;
                settings.FlowchartWindowTop = Position.Y;
            }
            if (Width > 0 && Height > 0)
            {
                settings.FlowchartWindowWidth = Width;
                settings.FlowchartWindowHeight = Height;
            }
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            // Mark flowchart as closed (#377)
            SettingsService.Instance.FlowchartWindowOpen = false;
        }

        /// <summary>
        /// Update the flowchart to display the given dialog
        /// </summary>
        /// <param name="dialog">The dialog to display</param>
        /// <param name="fileName">Optional filename for display</param>
        public void UpdateDialog(Dialog? dialog, string? fileName = null)
        {
            FlowchartPanelControl.UpdateDialog(dialog, fileName);

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
            FlowchartPanelControl.Clear();
            Title = "Flowchart View";
        }

        /// <summary>
        /// Select a node in the flowchart by DialogNode.
        /// Used for TreeView → Flowchart sync.
        /// </summary>
        public void SelectNode(DialogNode? dialogNode)
        {
            FlowchartPanelControl.SelectNode(dialogNode);
        }
    }
}
