using System;
using Avalonia;
using Avalonia.Controls;
using DialogEditor.Services;
using DialogEditor.Views;
using Radoub.Formats.Logging;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Flowchart layout modes: Floating, Side-by-Side, and Tabbed.
    /// </summary>
    public partial class FlowchartManager
    {
        /// <summary>
        /// Opens the floating flowchart window (F5 shortcut).
        /// </summary>
        public void OpenFloatingFlowchart()
        {
            try
            {
                // Use WindowLifecycleManager for flowchart window
                var flowchart = _windows.ShowOrActivate(
                    WindowKeys.Flowchart,
                    () =>
                    {
                        var w = new FlowchartWindow();
                        w.NodeClicked += OnFlowchartNodeClicked;
                        w.ContextMenuAction += OnFlowchartContextMenuAction; // #461: Context menu parity
                        w.SiblingReorderRequested += OnFlowchartSiblingReorder; // #240: Drag-drop reorder
                        w.ReparentRequested += OnFlowchartReparent; // #1965: Drag-drop reparent
                        w.ShortcutManager = _shortcutManager; // #809: Enable keyboard shortcuts
                        return w;
                    });

                // Update with current dialog
                flowchart.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);

                // Mark flowchart as open (#377)
                _settings.FlowchartWindowOpen = true;
                _settings.FlowchartVisible = true;

                ViewModel.StatusMessage = "Flowchart view opened";
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening flowchart: {ex.Message}");
                ViewModel.StatusMessage = "Error opening flowchart view";
            }
        }

        /// <summary>
        /// Applies the selected flowchart layout mode.
        /// </summary>
        public void ApplyLayout(string layoutValue)
        {
            _settings.FlowchartLayout = layoutValue;
            UpdateLayoutMenuChecks();
            ApplyLayoutInternal();
            ViewModel.StatusMessage = $"Flowchart layout set to {layoutValue}";
        }

        /// <summary>
        /// Updates the flowchart layout menu check marks.
        /// </summary>
        public void UpdateLayoutMenuChecks()
        {
            var currentLayout = _settings.FlowchartLayout;

            var floatingItem = _window.FindControl<MenuItem>("FlowchartLayoutFloating");
            var sideBySideItem = _window.FindControl<MenuItem>("FlowchartLayoutSideBySide");
            var tabbedItem = _window.FindControl<MenuItem>("FlowchartLayoutTabbed");

            if (floatingItem != null)
                floatingItem.Icon = currentLayout == "Floating" ? new TextBlock { Text = "✓" } : null;
            if (sideBySideItem != null)
                sideBySideItem.Icon = currentLayout == "SideBySide" ? new TextBlock { Text = "✓" } : null;
            if (tabbedItem != null)
                tabbedItem.Icon = currentLayout == "Tabbed" ? new TextBlock { Text = "✓" } : null;
        }

        /// <summary>
        /// Applies the current layout setting.
        /// </summary>
        private void ApplyLayoutInternal()
        {
            var layout = _settings.FlowchartLayout;

            // Close existing floating window if switching to embedded mode
            if (layout != "Floating")
            {
                _windows.Close(WindowKeys.Flowchart);
            }

            // Apply layout based on setting
            switch (layout)
            {
                case "SideBySide":
                    ShowSideBySideFlowchart();
                    break;
                case "Tabbed":
                    ShowTabbedFlowchart();
                    break;
                default: // "Floating"
                    HideEmbeddedFlowchart();
                    break;
            }
        }

        /// <summary>
        /// Shows the side-by-side (embedded) flowchart panel.
        /// </summary>
        public void ShowSideBySideFlowchart()
        {
            // Hide tabbed panel if it was showing
            HideTabbedFlowchart();

            // Use WithControls for coordinated multi-control updates
            var success = _controls.WithControls<Grid, GridSplitter, Border, FlowchartPanel>(
                "MainContentGrid", "FlowchartSplitter", "EmbeddedFlowchartBorder", "EmbeddedFlowchartPanel",
                (grid, splitter, border, panel) =>
                {
                    if (grid.ColumnDefinitions.Count < 5) return;

                    // Show columns (indices 3 and 4 are the splitter and panel columns)
                    // Use saved width or default (#377)
                    var savedWidth = _settings.FlowchartPanelWidth;
                    grid.ColumnDefinitions[3].Width = new GridLength(5);
                    grid.ColumnDefinitions[4].Width = new GridLength(savedWidth, GridUnitType.Pixel);
                    grid.ColumnDefinitions[4].MinWidth = 200;

                    // Show controls
                    splitter.IsVisible = true;
                    border.IsVisible = true;

                    // Wire up node click handler and keyboard shortcuts if not already done
                    if (!_embeddedFlowchartWired)
                    {
                        panel.NodeClicked += OnEmbeddedFlowchartNodeClicked;
                        panel.ContextMenuAction += OnFlowchartContextMenuAction; // #461: Context menu parity
                        panel.SiblingReorderRequested += OnFlowchartSiblingReorder; // #240: Drag-drop reorder
                        panel.ReparentRequested += OnFlowchartReparent; // #1965: Drag-drop reparent
                        panel.ShortcutManager = _shortcutManager; // #809: Enable keyboard shortcuts
                        _embeddedFlowchartWired = true;
                    }

                    // Watch for column width changes to save (#377)
                    grid.ColumnDefinitions[4].PropertyChanged += OnFlowchartColumnWidthChanged;

                    // Update with current dialog
                    panel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
                });

            if (success)
            {
                // Mark flowchart as visible (#377)
                _settings.FlowchartVisible = true;
                UnifiedLogger.LogUI(LogLevel.INFO, "Side-by-side flowchart panel shown");
            }
            else
            {
                UnifiedLogger.LogUI(LogLevel.WARN, "Failed to show Side-by-Side flowchart: one or more controls not found");
            }
        }

        /// <summary>
        /// Shows the tabbed flowchart panel.
        /// </summary>
        public void ShowTabbedFlowchart()
        {
            // Hide side-by-side panel if it was showing
            HideSideBySideFlowchart();

            // Use WithControls for coordinated multi-control updates
            var success = _controls.WithControls<TabItem, FlowchartPanel>(
                "FlowchartTab", "TabbedFlowchartPanel",
                (tab, panel) =>
                {
                    tab.IsVisible = true;

                    // Wire up node click handler and keyboard shortcuts if not already done
                    if (!_tabbedFlowchartWired)
                    {
                        panel.NodeClicked += OnTabbedFlowchartNodeClicked;
                        panel.ContextMenuAction += OnFlowchartContextMenuAction; // #461: Context menu parity
                        panel.SiblingReorderRequested += OnFlowchartSiblingReorder; // #240: Drag-drop reorder
                        panel.ReparentRequested += OnFlowchartReparent; // #1965: Drag-drop reparent
                        panel.ShortcutManager = _shortcutManager; // #809: Enable keyboard shortcuts
                        _tabbedFlowchartWired = true;
                    }

                    // Update with current dialog
                    panel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
                });

            if (success)
            {
                // Mark flowchart as visible (#377)
                _settings.FlowchartVisible = true;
                UnifiedLogger.LogUI(LogLevel.INFO, "Tabbed flowchart panel shown");
            }
            else
            {
                UnifiedLogger.LogUI(LogLevel.WARN, "Failed to show Tabbed flowchart: tab or panel not found");
            }
        }

        /// <summary>
        /// Hides the side-by-side flowchart panel.
        /// </summary>
        public void HideSideBySideFlowchart()
        {
            // Use WithControls for coordinated multi-control updates
            _controls.WithControls<Grid, GridSplitter, Border>(
                "MainContentGrid", "FlowchartSplitter", "EmbeddedFlowchartBorder",
                (grid, splitter, border) =>
                {
                    if (grid.ColumnDefinitions.Count < 5) return;

                    // Hide columns (indices 3 and 4 are the splitter and panel columns)
                    grid.ColumnDefinitions[3].Width = new GridLength(0);
                    grid.ColumnDefinitions[4].Width = new GridLength(0);
                    grid.ColumnDefinitions[4].MinWidth = 0;

                    // Hide controls
                    splitter.IsVisible = false;
                    border.IsVisible = false;
                });
        }

        /// <summary>
        /// Hides the tabbed flowchart panel.
        /// </summary>
        public void HideTabbedFlowchart()
        {
            if (_controls.SetVisible("FlowchartTab", false))
            {
                UnifiedLogger.LogUI(LogLevel.INFO, "Tabbed flowchart panel hidden");
            }
        }

        /// <summary>
        /// Hides all embedded flowchart panels (side-by-side and tabbed).
        /// </summary>
        public void HideEmbeddedFlowchart()
        {
            HideSideBySideFlowchart();
            HideTabbedFlowchart();
            // Mark flowchart as not visible (#377)
            _settings.FlowchartVisible = false;
            UnifiedLogger.LogUI(LogLevel.INFO, "All embedded flowchart panels hidden");
        }

        /// <summary>
        /// Restores flowchart visibility on startup based on saved settings (#377).
        /// </summary>
        public void RestoreOnStartup()
        {
            var layout = _settings.FlowchartLayout;
            UnifiedLogger.LogUI(LogLevel.INFO, $"Restoring flowchart on startup: layout={layout}");

            switch (layout)
            {
                case "Floating":
                    OpenFloatingFlowchart();
                    break;
                case "SideBySide":
                    ShowSideBySideFlowchart();
                    break;
                case "Tabbed":
                    ShowTabbedFlowchart();
                    break;
            }
        }

        private void OnFlowchartColumnWidthChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "Width" && sender is ColumnDefinition colDef && colDef.Width.IsAbsolute)
            {
                _settings.FlowchartPanelWidth = colDef.Width.Value;
            }
        }
    }
}
