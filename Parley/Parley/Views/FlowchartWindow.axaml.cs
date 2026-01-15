using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.Utils;

namespace DialogEditor.Views
{
    /// <summary>
    /// Window wrapper for FlowchartPanel.
    /// Used for "Floating Window" layout mode.
    /// </summary>
    public partial class FlowchartWindow : Window
    {
        private bool _isRestoringPosition = false;

        /// <summary>
        /// Raised when a flowchart node is clicked.
        /// The FlowchartNode parameter contains the clicked node with context (IsLink, OriginalPointer, etc.)
        /// </summary>
        public event EventHandler<FlowchartNode?>? NodeClicked;

        /// <summary>
        /// Raised when a context menu action is requested (#461).
        /// </summary>
        public event EventHandler<FlowchartContextMenuEventArgs>? ContextMenuAction;

        public FlowchartWindow()
        {
            InitializeComponent();

            // Forward node click events from the panel
            FlowchartPanelControl.NodeClicked += (sender, node) => NodeClicked?.Invoke(this, node);

            // Forward context menu action events from the panel (#461)
            FlowchartPanelControl.ContextMenuAction += (sender, args) => ContextMenuAction?.Invoke(this, args);

            // Restore window position after window opens (Screens not available in constructor)
            Opened += async (s, e) => await RestoreWindowPositionAsync();

            // Save position when window moves or resizes
            PositionChanged += OnPositionChanged;
            PropertyChanged += OnPropertyChanged;
            Closed += OnWindowClosed;
        }

        /// <summary>
        /// Restore window position and size from settings (#377)
        /// </summary>
        private async Task RestoreWindowPositionAsync()
        {
            _isRestoringPosition = true;
            var settings = SettingsService.Instance;

            UnifiedLogger.LogUI(LogLevel.DEBUG,
                $"Restoring flowchart position: Left={settings.FlowchartWindowLeft}, Top={settings.FlowchartWindowTop}");

            // Restore size first
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
                    UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart position restored to ({targetPos.X}, {targetPos.Y})");
                }
                else
                {
                    UnifiedLogger.LogUI(LogLevel.WARN,
                        $"Saved flowchart position ({targetPos.X}, {targetPos.Y}) is off-screen, using default");
                }
            }

            // Allow position saving after a short delay (to avoid saving the restore itself)
            await Task.Delay(500);
            _isRestoringPosition = false;
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
            if (!_isRestoringPosition)
            {
                SaveWindowPosition();
            }
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!_isRestoringPosition && (e.Property == WidthProperty || e.Property == HeightProperty))
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
            UnifiedLogger.LogUI(LogLevel.DEBUG,
                $"Saved flowchart position: ({Position.X}, {Position.Y}), size: ({Width}x{Height})");
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            // Mark flowchart as closed (#377)
            SettingsService.Instance.FlowchartWindowOpen = false;
            UnifiedLogger.LogUI(LogLevel.DEBUG, "Flowchart window closed, FlowchartWindowOpen = false");
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

        /// <summary>
        /// Sets the keyboard shortcut manager for the embedded panel.
        /// #809: Enables keyboard parity with TreeView in floating window.
        /// </summary>
        public KeyboardShortcutManager? ShortcutManager
        {
            get => FlowchartPanelControl.ShortcutManager;
            set => FlowchartPanelControl.ShortcutManager = value;
        }
    }
}
