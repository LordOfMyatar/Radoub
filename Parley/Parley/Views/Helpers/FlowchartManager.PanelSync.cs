using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Views;
using Radoub.Formats.Logging;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Panel updates: syncing flowchart panels with dialog changes.
    /// </summary>
    public partial class FlowchartManager
    {
        /// <summary>
        /// Updates all flowchart panels (floating, embedded, tabbed) with current dialog.
        /// Called when dialog structure changes to keep FlowView in sync with TreeView.
        /// </summary>
        public void UpdateAllPanels()
        {
            if (ViewModel.CurrentDialog == null)
                return;

            // Update floating flowchart window if open
            _windows.WithWindow<FlowchartWindow>(WindowKeys.Flowchart, w =>
            {
                w.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
            });

            // Update embedded panel (side-by-side layout)
            var embeddedPanel = _window.FindControl<FlowchartPanel>("EmbeddedFlowchartPanel");
            if (embeddedPanel != null)
            {
                embeddedPanel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
            }

            // Update tabbed panel
            var tabbedPanel = _window.FindControl<FlowchartPanel>("TabbedFlowchartPanel");
            if (tabbedPanel != null)
            {
                tabbedPanel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
            }
        }

        /// <summary>
        /// Updates all flowchart views after a dialog is loaded.
        /// Handles floating window, side-by-side, and tabbed layouts (#394).
        /// </summary>
        public void UpdateAfterLoad()
        {
            var layout = _settings.FlowchartLayout;
            var embeddedBorder = _window.FindControl<Border>("EmbeddedFlowchartBorder");
            var embeddedPanel = _window.FindControl<FlowchartPanel>("EmbeddedFlowchartPanel");
            var flowchartTab = _window.FindControl<TabItem>("FlowchartTab");
            var tabbedPanel = _window.FindControl<FlowchartPanel>("TabbedFlowchartPanel");

            // Update floating window if open (#394)
            _windows.WithWindow<FlowchartWindow>(WindowKeys.Flowchart, w =>
            {
                w.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
                UnifiedLogger.LogUI(LogLevel.DEBUG, "Floating flowchart updated after dialog load");
            });

            if (layout == "SideBySide" && embeddedBorder?.IsVisible == true && embeddedPanel != null)
            {
                embeddedPanel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
            }
            else if (layout == "Tabbed" && flowchartTab?.IsVisible == true && tabbedPanel != null)
            {
                tabbedPanel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
            }
        }

        /// <summary>
        /// Clears all flowchart views when a dialog file is closed (#378).
        /// </summary>
        public void ClearAll()
        {
            // Clear floating window if open
            _windows.WithWindow<FlowchartWindow>(WindowKeys.Flowchart, w =>
            {
                w.Clear();
                UnifiedLogger.LogUI(LogLevel.DEBUG, "Floating flowchart cleared");
            });

            // Clear embedded panel (side-by-side layout)
            var embeddedPanel = _window.FindControl<FlowchartPanel>("EmbeddedFlowchartPanel");
            if (embeddedPanel != null)
            {
                embeddedPanel.Clear();
            }

            // Clear tabbed panel
            var tabbedPanel = _window.FindControl<FlowchartPanel>("TabbedFlowchartPanel");
            if (tabbedPanel != null)
            {
                tabbedPanel.Clear();
            }

            UnifiedLogger.LogUI(LogLevel.DEBUG, "All flowchart views cleared");
        }

        /// <summary>
        /// Syncs selection to all flowchart panels when TreeView selection changes.
        /// </summary>
        public void SyncSelectionToFlowcharts(DialogNode? originalNode)
        {
            // Sync to floating window
            _windows.WithWindow<FlowchartWindow>(WindowKeys.Flowchart, w => w.SelectNode(originalNode));

            // Sync to embedded panel
            var embeddedBorder = _window.FindControl<Border>("EmbeddedFlowchartBorder");
            var embeddedPanel = _window.FindControl<FlowchartPanel>("EmbeddedFlowchartPanel");
            if (embeddedBorder?.IsVisible == true && embeddedPanel != null)
            {
                embeddedPanel.SelectNode(originalNode);
            }

            // Sync to tabbed panel
            var flowchartTab = _window.FindControl<TabItem>("FlowchartTab");
            var tabbedPanel = _window.FindControl<FlowchartPanel>("TabbedFlowchartPanel");
            if (tabbedPanel != null && flowchartTab?.IsVisible == true)
            {
                tabbedPanel.SelectNode(originalNode);
            }
        }
    }
}
