using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Utils;
using DialogEditor.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DialogEditor.Services
{
    /// <summary>
    /// Handles smart node creation with debouncing, tree navigation, and selection management.
    /// Extracted from MainWindow.axaml.cs to separate business logic from UI coordination.
    /// </summary>
    public class NodeCreationHelper
    {
        private readonly MainViewModel _viewModel;
        private readonly Func<string, Control?> _findControl;
        private readonly Action _saveCurrentNodeProperties;
        private readonly Action _triggerAutoSave;

        // Debouncing state (Issue #76)
        private DateTime _lastAddNodeTime = DateTime.MinValue;
        private bool _isAddingNode = false;
        private const int ADD_NODE_DEBOUNCE_MS = 150;

        public NodeCreationHelper(
            MainViewModel viewModel,
            Func<string, Control?> findControl,
            Action saveCurrentNodeProperties,
            Action triggerAutoSave)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _findControl = findControl ?? throw new ArgumentNullException(nameof(findControl));
            _saveCurrentNodeProperties = saveCurrentNodeProperties ?? throw new ArgumentNullException(nameof(saveCurrentNodeProperties));
            _triggerAutoSave = triggerAutoSave ?? throw new ArgumentNullException(nameof(triggerAutoSave));
        }

        /// <summary>
        /// Creates a smart node with debouncing, selection management, and auto-focus
        /// </summary>
        public async Task CreateSmartNodeAsync(TreeViewSafeNode? selectedNode)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "=== CreateSmartNodeAsync CALLED ===");

            // DEBOUNCE CHECK: Prevent rapid Ctrl+D causing focus misplacement (Issue #76)
            var timeSinceLastAdd = (DateTime.Now - _lastAddNodeTime).TotalMilliseconds;
            if (timeSinceLastAdd < ADD_NODE_DEBOUNCE_MS)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"CreateSmartNodeAsync: Debounced (only {timeSinceLastAdd:F0}ms since last add, minimum {ADD_NODE_DEBOUNCE_MS}ms required)");
                return;
            }

            // OVERLAP CHECK: Prevent concurrent node creation operations (Issue #76)
            if (_isAddingNode)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    "CreateSmartNodeAsync: Operation already in progress, rejecting concurrent call");
                return;
            }

            _lastAddNodeTime = DateTime.Now;
            _isAddingNode = true;

            try
            {
                // IMPORTANT: Save current node properties before creating new node
                // This ensures any typed text is saved before moving to next node
                _saveCurrentNodeProperties();
                UnifiedLogger.LogApplication(LogLevel.INFO, "CreateSmartNodeAsync: Saved current node properties");

                // Track dialog node count before adding
                int entryCountBefore = _viewModel.CurrentDialog?.Entries.Count ?? 0;
                int replyCountBefore = _viewModel.CurrentDialog?.Replies.Count ?? 0;

                _viewModel.AddSmartNode(selectedNode);

                // Wait for tree view to refresh
                await Task.Delay(100);

                // Find and select the newly created node
                var treeView = _findControl("DialogTreeView") as TreeView;
                if (treeView != null && _viewModel.CurrentDialog != null)
                {
                    bool entryAdded = _viewModel.CurrentDialog.Entries.Count > entryCountBefore;
                    bool replyAdded = _viewModel.CurrentDialog.Replies.Count > replyCountBefore;

                    if (entryAdded || replyAdded)
                    {
                        var newNode = FindLastAddedNode(treeView, entryAdded, replyAdded);
                        if (newNode != null)
                        {
                            // Expand all ancestor nodes to make new node visible (Issue #7)
                            ExpandToNode(treeView, newNode);

                            treeView.SelectedItem = newNode;
                            UnifiedLogger.LogApplication(LogLevel.INFO, "CreateSmartNodeAsync: Selected new node in tree");
                        }
                    }
                }

                // Auto-focus to text box for immediate typing
                // Delay allows tree view selection and properties panel population to complete
                UnifiedLogger.LogApplication(LogLevel.INFO, "CreateSmartNodeAsync: Waiting 300ms for properties panel...");
                await Task.Delay(300);

                var textTextBox = _findControl("TextTextBox") as TextBox;
                if (textTextBox != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, "CreateSmartNodeAsync: Attempting to focus TextTextBox");

                    // Try multiple times to overcome focus stealing
                    textTextBox.Focus();
                    await Task.Delay(50);
                    textTextBox.Focus();
                    textTextBox.SelectAll();

                    UnifiedLogger.LogApplication(LogLevel.INFO, $"CreateSmartNodeAsync: Focus set, IsFocused={textTextBox.IsFocused}");
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR, "CreateSmartNodeAsync: TextTextBox control not found!");
                }

                // Trigger auto-save after node creation
                _triggerAutoSave();
            }
            finally
            {
                // Reset flag to allow next operation (Issue #76)
                _isAddingNode = false;
            }
        }

        /// <summary>
        /// Finds the last added node in the tree
        /// </summary>
        public TreeViewSafeNode? FindLastAddedNode(TreeView treeView, bool entryAdded, bool replyAdded)
        {
            if (treeView.ItemsSource == null) return null;

            foreach (var item in treeView.ItemsSource)
            {
                if (item is TreeViewSafeNode node)
                {
                    var found = FindLastAddedNodeRecursive(node, entryAdded, replyAdded);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private TreeViewSafeNode? FindLastAddedNodeRecursive(TreeViewSafeNode node, bool entryAdded, bool replyAdded)
        {
            // Check children first (depth-first to find last node)
            if (node.Children != null)
            {
                foreach (var child in node.Children.Reverse())
                {
                    var found = FindLastAddedNodeRecursive(child, entryAdded, replyAdded);
                    if (found != null) return found;
                }
            }

            // Check if this is the node we're looking for
            if (entryAdded && node.OriginalNode.Type == DialogNodeType.Entry)
            {
                return node;
            }
            if (replyAdded && node.OriginalNode.Type == DialogNodeType.Reply)
            {
                return node;
            }

            return null;
        }

        /// <summary>
        /// Expands all ancestor nodes to make target node visible (Issue #7)
        /// </summary>
        public void ExpandToNode(TreeView treeView, TreeViewSafeNode targetNode)
        {
            var parent = FindParentNode(treeView, targetNode);
            if (parent != null)
            {
                parent.IsExpanded = true;
                ExpandToNode(treeView, parent); // Recurse upward
            }
        }

        /// <summary>
        /// Finds the parent node of a given child node in the tree (Issue #7)
        /// </summary>
        public TreeViewSafeNode? FindParentNode(TreeView treeView, TreeViewSafeNode targetNode)
        {
            if (treeView.ItemsSource == null) return null;

            foreach (var item in treeView.ItemsSource)
            {
                if (item is TreeViewSafeNode node)
                {
                    var parent = FindParentNodeRecursive(node, targetNode);
                    if (parent != null) return parent;
                }
            }
            return null;
        }

        private TreeViewSafeNode? FindParentNodeRecursive(TreeViewSafeNode currentNode, TreeViewSafeNode targetNode)
        {
            if (currentNode.Children == null) return null;

            // Check if targetNode is a direct child
            if (currentNode.Children.Contains(targetNode))
            {
                return currentNode;
            }

            // Recurse through children
            foreach (var child in currentNode.Children)
            {
                var found = FindParentNodeRecursive(child, targetNode);
                if (found != null) return found;
            }

            return null;
        }
    }
}
