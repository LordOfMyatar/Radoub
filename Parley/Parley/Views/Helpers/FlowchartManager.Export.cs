using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DialogEditor.Services;
using DialogEditor.Views;
using Radoub.Formats.Logging;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// PNG export functionality for flowcharts.
    /// </summary>
    public partial class FlowchartManager
    {
        /// <summary>
        /// Exports the flowchart to PNG format.
        /// </summary>
        public async Task ExportToPngAsync()
        {
            try
            {
                // Get the active flowchart panel
                FlowchartPanel? activePanel = GetActivePanel();
                if (activePanel == null)
                {
                    ViewModel.StatusMessage = "No flowchart to export. Open a dialog first.";
                    return;
                }

                // Set up file picker
                var storageProvider = _window.StorageProvider;

                var options = new FilePickerSaveOptions
                {
                    Title = "Export Flowchart as PNG",
                    SuggestedFileName = string.IsNullOrEmpty(ViewModel.CurrentFileName)
                        ? "flowchart.png"
                        : System.IO.Path.GetFileNameWithoutExtension(ViewModel.CurrentFileName) + "_flowchart.png",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } }
                    }
                };

                var file = await storageProvider.SaveFilePickerAsync(options);
                if (file == null) return;

                var filePath = file.Path.LocalPath;

                var success = await activePanel.ExportToPngAsync(filePath);

                if (success)
                {
                    ViewModel.StatusMessage = $"Flowchart exported to {System.IO.Path.GetFileName(filePath)}";
                }
                else
                {
                    ViewModel.StatusMessage = "Export failed. Check logs for details.";
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Export flowchart failed: {ex.Message}");
                ViewModel.StatusMessage = $"Export failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets the currently active flowchart panel for export.
        /// </summary>
        public FlowchartPanel? GetActivePanel()
        {
            var layout = _settings.FlowchartLayout;
            var embeddedBorder = _window.FindControl<Border>("EmbeddedFlowchartBorder");
            var embeddedPanel = _window.FindControl<FlowchartPanel>("EmbeddedFlowchartPanel");
            var flowchartTab = _window.FindControl<TabItem>("FlowchartTab");
            var tabbedPanel = _window.FindControl<FlowchartPanel>("TabbedFlowchartPanel");

            switch (layout)
            {
                case "SideBySide":
                    if (embeddedBorder?.IsVisible == true && embeddedPanel?.ViewModel?.HasContent == true)
                        return embeddedPanel;
                    break;
                case "Tabbed":
                    if (flowchartTab?.IsVisible == true && tabbedPanel?.ViewModel?.HasContent == true)
                        return tabbedPanel;
                    break;
            }

            // For Floating mode or if embedded panels don't have content,
            // check if we can use the floating window
            if (_windows.IsOpen(WindowKeys.Flowchart))
            {
                // FlowchartWindow doesn't expose FlowchartPanel directly, so we need to update it
                // For now, just return the embedded panel if it has content
                if (embeddedBorder?.IsVisible == true && embeddedPanel?.ViewModel?.HasContent == true)
                    return embeddedPanel;
                if (flowchartTab?.IsVisible == true && tabbedPanel?.ViewModel?.HasContent == true)
                    return tabbedPanel;
            }

            // If no embedded panel is visible but we have a dialog loaded, use the side-by-side panel
            // (it's always in the XAML, just hidden)
            if (ViewModel.CurrentDialog != null && embeddedPanel != null)
            {
                // Temporarily update the embedded panel for export
                embeddedPanel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
                return embeddedPanel;
            }

            return null;
        }
    }
}
