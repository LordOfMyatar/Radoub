using System;
using System.Collections.Generic;
using System.Linq;
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
        private FlowchartGraph? _flowchartGraph;

        [ObservableProperty]
        private Graph? _graph;

        [ObservableProperty]
        private string _statusText = "No dialog loaded";

        [ObservableProperty]
        private bool _hasContent;

        [ObservableProperty]
        private string? _fileName;

        [ObservableProperty]
        private string? _selectedNodeId;

        /// <summary>
        /// Gets the underlying FlowchartGraph for export purposes
        /// </summary>
        public FlowchartGraph? FlowchartGraph => _flowchartGraph;

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

                // Store for later lookup
                _flowchartGraph = flowchartGraph;

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
        /// Forces a refresh of the graph to update visual styling (e.g., after settings change)
        /// </summary>
        public void RefreshGraph()
        {
            if (_flowchartGraph == null || Graph == null)
                return;

            // Re-create the Avalonia graph to force DataTemplate re-evaluation
            // This triggers the converters to re-run with updated settings
            var currentGraph = Graph;
            Graph = null;
            Graph = currentGraph;

            OnPropertyChanged(nameof(Graph));
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
            _flowchartGraph = null;
            SelectedNodeId = null;
        }

        /// <summary>
        /// Finds the FlowchartNode ID for a given DialogNode
        /// </summary>
        public string? FindNodeIdForDialogNode(DialogNode? dialogNode)
        {
            if (dialogNode == null || _flowchartGraph == null)
                return null;

            var flowchartNode = _flowchartGraph.Nodes.Values
                .FirstOrDefault(n => n.OriginalNode == dialogNode);

            return flowchartNode?.Id;
        }

        /// <summary>
        /// Selects a node in the flowchart by DialogNode
        /// </summary>
        public void SelectNode(DialogNode? dialogNode)
        {
            SelectedNodeId = FindNodeIdForDialogNode(dialogNode);
            if (SelectedNodeId != null)
            {
                UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart node selected: {SelectedNodeId}");
            }
        }
    }
}
