using System;
using AvaloniaGraphControl;
using CommunityToolkit.Mvvm.ComponentModel;
using DialogEditor.Models;
using DialogEditor.Services;

namespace DialogEditor.ViewModels
{
    /// <summary>
    /// ViewModel for the FlowchartPanel, manages dialog-to-graph conversion and display state.
    /// </summary>
    public partial class FlowchartPanelViewModel : ViewModelBase
    {
        private readonly DialogToFlowchartConverter _converter = new();

        [ObservableProperty]
        private Graph? _graph;

        [ObservableProperty]
        private string _statusText = "No dialog loaded";

        [ObservableProperty]
        private bool _hasContent;

        [ObservableProperty]
        private string? _fileName;

        /// <summary>
        /// Update the flowchart to display the given dialog
        /// </summary>
        /// <param name="dialog">The dialog to display</param>
        /// <param name="fileName">Optional filename for display</param>
        public void UpdateDialog(Dialog? dialog, string? fileName = null)
        {
            FileName = fileName;

            if (dialog == null)
            {
                Graph = null;
                StatusText = "No dialog loaded";
                HasContent = false;
                return;
            }

            try
            {
                // Convert dialog to our flowchart format
                var flowchartGraph = _converter.Convert(dialog, fileName);

                if (flowchartGraph.IsEmpty)
                {
                    Graph = null;
                    StatusText = "Dialog is empty";
                    HasContent = false;
                    return;
                }

                // Convert to AvaloniaGraphControl format
                Graph = FlowchartGraphAdapter.ToAvaloniaGraph(flowchartGraph);
                StatusText = $"{flowchartGraph.Nodes.Count} nodes, {flowchartGraph.Edges.Count} edges";
                HasContent = true;

                UnifiedLogger.LogUI(LogLevel.INFO, $"Flowchart updated: {StatusText}");
            }
            catch (Exception ex)
            {
                Graph = null;
                StatusText = $"Error: {ex.Message}";
                HasContent = false;
                UnifiedLogger.LogUI(LogLevel.ERROR, $"Flowchart conversion failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear the flowchart display
        /// </summary>
        public void Clear()
        {
            Graph = null;
            StatusText = "No dialog loaded";
            HasContent = false;
            FileName = null;
        }
    }
}
