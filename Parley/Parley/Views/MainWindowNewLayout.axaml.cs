using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogEditor.Services;
using System;

namespace DialogEditor.Views
{
    /// <summary>
    /// New layout window with resizable panels
    /// Inherits from MainWindow to reuse all event handlers
    /// </summary>
    public partial class MainWindowNewLayout : MainWindow
    {
        private bool _pluginPanelCollapsed = false;
        private double _savedPluginColumnWidth = 200;

        public MainWindowNewLayout()
        {
            InitializeComponent();
            UnifiedLogger.LogApplication(LogLevel.INFO, "MainWindowNewLayout initialized with resizable panels");
        }

        /// <summary>
        /// Toggle plugin panel visibility
        /// </summary>
        private void OnTogglePluginPanel(object? sender, RoutedEventArgs e)
        {
            try
            {
                var mainGrid = this.FindControl<Grid>("MainGrid");
                if (mainGrid == null) return;

                var topGrid = mainGrid.Children[1] as Grid;
                if (topGrid == null) return;

                var innerGrid = topGrid.Children[0] as Grid;
                if (innerGrid == null) return;

                // Plugin panel is in column 6
                var pluginColumn = innerGrid.ColumnDefinitions[6];
                var button = sender as Button;

                if (_pluginPanelCollapsed)
                {
                    // Restore plugin panel
                    pluginColumn.Width = new GridLength(_savedPluginColumnWidth, GridUnitType.Pixel);
                    if (button != null)
                    {
                        button.Content = "≪";
                        ToolTip.SetTip(button, "Hide Plugin Panel");
                    }
                    _pluginPanelCollapsed = false;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Plugin panel expanded");
                }
                else
                {
                    // Collapse plugin panel
                    _savedPluginColumnWidth = pluginColumn.ActualWidth;
                    pluginColumn.Width = new GridLength(0);
                    if (button != null)
                    {
                        button.Content = "≫";
                        ToolTip.SetTip(button, "Show Plugin Panel");
                    }
                    _pluginPanelCollapsed = true;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Plugin panel collapsed");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error toggling plugin panel: {ex.Message}");
            }
        }

        /// <summary>
        /// Override InitializeComponent to handle the new layout structure
        /// </summary>
        private new void InitializeComponent()
        {
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

            // Additional initialization for new layout-specific controls can go here
            // The base class constructor will handle all the standard initialization
        }
    }
}