using System;
using System.Collections.Generic;
using System.Linq;
using AvaloniaGraphControl;
using CommunityToolkit.Mvvm.ComponentModel;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.ViewModels
{
    /// <summary>
    /// ViewModel for the FlowchartPanel, manages dialog-to-graph conversion and display state.
    /// </summary>
    public partial class FlowchartPanelViewModel : ViewModelBase
    {
        private readonly DialogToFlowchartConverter _converter = new();
        private FlowchartGraph? _flowchartGraph;
        private Dialog? _sourceDialog; // Keep source for refresh (#340)
        private readonly HashSet<string> _collapsedNodeIds = new(); // Track collapsed nodes (#251)
        private bool _isHandlingExternalEvent; // Prevent event loops (#451)

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
        /// Maximum lines to display in flowchart nodes before truncation (#813).
        /// Bound from UISettingsService for XAML data binding.
        /// </summary>
        public int NodeMaxLines => UISettingsService.Instance.FlowchartNodeMaxLines;

        /// <summary>
        /// Notifies that a property has changed. Used by FlowchartPanel to trigger refresh on settings change.
        /// </summary>
        public new void OnPropertyChanged(string propertyName) => base.OnPropertyChanged(propertyName);

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
                _sourceDialog = null;
                return;
            }

            // Store source for refresh (#340)
            _sourceDialog = dialog;

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
            if (_sourceDialog == null)
                return;

            // Re-convert from source Dialog to get fresh FlowchartNode objects (#340)
            // This forces Avalonia DataTemplates to re-evaluate converters with new colors
            try
            {
                var flowchartGraph = _converter.Convert(_sourceDialog, FileName);
                if (!flowchartGraph.IsEmpty)
                {
                    _flowchartGraph = flowchartGraph;
                    Graph = null;
                    Graph = FlowchartGraphAdapter.ToAvaloniaGraph(flowchartGraph);
                    UnifiedLogger.LogUI(LogLevel.DEBUG, "Flowchart fully rebuilt for color refresh");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogUI(LogLevel.ERROR, $"Flowchart refresh failed: {ex.Message}");
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
            _flowchartGraph = null;
            _sourceDialog = null;
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

        #region Collapse/Expand Support (#251)

        /// <summary>
        /// Toggles the collapsed state of a node's children.
        /// When collapsed, the node's subtree is hidden in the flowchart.
        /// Publishes event to DialogChangeEventBus for TreeView sync (#451).
        /// </summary>
        public void ToggleCollapse(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return;

            // Find the DialogNode to publish the event
            var flowchartNode = _flowchartGraph?.Nodes.GetValueOrDefault(nodeId);
            var dialogNode = flowchartNode?.OriginalNode;

            if (_collapsedNodeIds.Contains(nodeId))
            {
                _collapsedNodeIds.Remove(nodeId);
                UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart node expanded: {nodeId}");

                // Publish expand event for TreeView sync (unless handling external event)
                if (!_isHandlingExternalEvent && dialogNode != null)
                {
                    DialogChangeEventBus.Instance.PublishNodeExpanded(dialogNode, "FlowView");
                }
            }
            else
            {
                _collapsedNodeIds.Add(nodeId);
                UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart node collapsed: {nodeId}");

                // Publish collapse event for TreeView sync (unless handling external event)
                if (!_isHandlingExternalEvent && dialogNode != null)
                {
                    DialogChangeEventBus.Instance.PublishNodeCollapsed(dialogNode, "FlowView");
                }
            }

            // Rebuild graph with updated collapse state
            RebuildGraphWithCollapseState();
        }

        /// <summary>
        /// Collapses a specific node's children.
        /// </summary>
        public void CollapseNode(string nodeId)
        {
            if (!string.IsNullOrEmpty(nodeId) && !_collapsedNodeIds.Contains(nodeId))
            {
                _collapsedNodeIds.Add(nodeId);
                RebuildGraphWithCollapseState();
            }
        }

        /// <summary>
        /// Expands a specific node's children.
        /// </summary>
        public void ExpandNode(string nodeId)
        {
            if (!string.IsNullOrEmpty(nodeId) && _collapsedNodeIds.Contains(nodeId))
            {
                _collapsedNodeIds.Remove(nodeId);
                RebuildGraphWithCollapseState();
            }
        }

        /// <summary>
        /// Collapses all nodes in the flowchart.
        /// Publishes AllCollapsed event for TreeView sync (#451).
        /// </summary>
        public void CollapseAll()
        {
            if (_flowchartGraph == null)
                return;

            // Add all nodes that have children to collapsed set
            foreach (var node in _flowchartGraph.Nodes.Values)
            {
                if (node.ChildCount > 0)
                    _collapsedNodeIds.Add(node.Id);
            }

            RebuildGraphWithCollapseState();
            UnifiedLogger.LogUI(LogLevel.DEBUG, "Flowchart: All nodes collapsed");

            // Publish all-collapsed event for TreeView sync (unless handling external event)
            if (!_isHandlingExternalEvent)
            {
                DialogChangeEventBus.Instance.PublishAllCollapsed("FlowView");
            }
        }

        /// <summary>
        /// Expands all nodes in the flowchart.
        /// Publishes AllExpanded event for TreeView sync (#451).
        /// </summary>
        public void ExpandAll()
        {
            _collapsedNodeIds.Clear();
            RebuildGraphWithCollapseState();
            UnifiedLogger.LogUI(LogLevel.DEBUG, "Flowchart: All nodes expanded");

            // Publish all-expanded event for TreeView sync (unless handling external event)
            if (!_isHandlingExternalEvent)
            {
                DialogChangeEventBus.Instance.PublishAllExpanded("FlowView");
            }
        }

        /// <summary>
        /// Checks if a node is currently collapsed.
        /// </summary>
        public bool IsNodeCollapsed(string nodeId)
        {
            return _collapsedNodeIds.Contains(nodeId);
        }

        /// <summary>
        /// Handles collapse/expand events from other views (TreeView).
        /// Called by FlowchartPanel when it receives DialogChangeEventBus events.
        /// </summary>
        public void HandleExternalCollapseEvent(DialogChangeEventArgs args)
        {
            if (args.Context == "FlowView")
                return; // Ignore events we published ourselves

            try
            {
                _isHandlingExternalEvent = true;

                switch (args.ChangeType)
                {
                    case DialogChangeType.NodeCollapsed:
                        if (args.AffectedNode != null)
                        {
                            var nodeId = FindNodeIdForDialogNode(args.AffectedNode);
                            if (nodeId != null)
                                CollapseNode(nodeId);
                        }
                        break;

                    case DialogChangeType.NodeExpanded:
                        if (args.AffectedNode != null)
                        {
                            var nodeId = FindNodeIdForDialogNode(args.AffectedNode);
                            if (nodeId != null)
                                ExpandNode(nodeId);
                        }
                        break;

                    case DialogChangeType.AllCollapsed:
                        CollapseAll();
                        break;

                    case DialogChangeType.AllExpanded:
                        ExpandAll();
                        break;
                }
            }
            finally
            {
                _isHandlingExternalEvent = false;
            }
        }

        /// <summary>
        /// Rebuilds the graph while respecting collapsed states.
        /// </summary>
        private void RebuildGraphWithCollapseState()
        {
            if (_flowchartGraph == null)
                return;

            try
            {
                // Update IsCollapsed property on nodes
                foreach (var node in _flowchartGraph.Nodes.Values)
                {
                    node.IsCollapsed = _collapsedNodeIds.Contains(node.Id);
                }

                // Build filtered graph excluding collapsed subtrees
                var filteredGraph = BuildFilteredGraph(_flowchartGraph);
                Graph = FlowchartGraphAdapter.ToAvaloniaGraph(filteredGraph);

                var hiddenCount = _flowchartGraph.Nodes.Count - filteredGraph.Nodes.Count;
                if (hiddenCount > 0)
                {
                    StatusText = $"{filteredGraph.Nodes.Count} nodes shown ({hiddenCount} hidden)";
                }
                else
                {
                    StatusText = $"{filteredGraph.Nodes.Count} nodes, {filteredGraph.Edges.Count} edges";
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogUI(LogLevel.ERROR, $"Flowchart rebuild failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a filtered graph that excludes collapsed subtrees.
        /// </summary>
        private FlowchartGraph BuildFilteredGraph(FlowchartGraph source)
        {
            var filtered = new FlowchartGraph { SourceFileName = source.SourceFileName };

            // Track which nodes should be hidden (descendants of collapsed nodes)
            var hiddenNodes = new HashSet<string>();

            // First pass: identify all nodes that should be hidden
            foreach (var nodeId in _collapsedNodeIds)
            {
                MarkDescendantsHidden(source, nodeId, hiddenNodes);
            }

            // Second pass: add visible nodes
            foreach (var kvp in source.Nodes)
            {
                if (!hiddenNodes.Contains(kvp.Key))
                {
                    filtered.AddNode(kvp.Value);
                }
            }

            // Third pass: add edges between visible nodes
            foreach (var edge in source.Edges)
            {
                if (!hiddenNodes.Contains(edge.SourceId) && !hiddenNodes.Contains(edge.TargetId))
                {
                    filtered.AddEdge(edge);
                }
            }

            // Add root node IDs (only if visible)
            foreach (var rootId in source.RootNodeIds)
            {
                if (!hiddenNodes.Contains(rootId))
                {
                    filtered.RootNodeIds.Add(rootId);
                }
            }

            return filtered;
        }

        /// <summary>
        /// Recursively marks all descendants of a node as hidden.
        /// Link nodes ARE hidden (they're children too), but we don't traverse into them
        /// since they're references to nodes elsewhere in the graph.
        /// </summary>
        private void MarkDescendantsHidden(FlowchartGraph graph, string parentId, HashSet<string> hiddenNodes)
        {
            foreach (var edge in graph.GetOutgoingEdges(parentId))
            {
                var childId = edge.TargetId;
                if (!hiddenNodes.Contains(childId))
                {
                    // Hide this child (including link nodes - they should collapse too)
                    hiddenNodes.Add(childId);

                    // Only recurse into non-link nodes
                    // Link nodes are terminal - their target is elsewhere in the graph
                    if (graph.Nodes.TryGetValue(childId, out var childNode) && !childNode.IsLink)
                    {
                        MarkDescendantsHidden(graph, childId, hiddenNodes);
                    }
                }
            }
        }

        #endregion
    }
}
