using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DialogEditor.Utils;
using DialogEditor.Services;

namespace DialogEditor.Models
{
    /// <summary>
    /// A wrapper for DialogNode that provides circular reference protection for WPF TreeView
    /// without modifying the original dialog data (preserving export integrity)
    /// </summary>
    public class TreeViewSafeNode : INotifyPropertyChanged
    {
        private readonly DialogNode _originalNode;
        private readonly DialogPtr? _sourcePointer; // The pointer that led to this node
        private readonly HashSet<DialogNode> _ancestorNodes;
        protected readonly int _depth;
        private ObservableCollection<TreeViewSafeNode>? _children;
        private bool _isExpanded;
        private readonly bool _isUnreachableSibling; // Issue #484: Warning for unreachable NPC entries

        // Global tracking of expanded nodes to show links instead of duplicating content
        private static readonly HashSet<DialogNode> _globalExpandedNodes = new();

        /// <summary>
        /// Flag to suppress event publishing during programmatic expand/collapse operations (#451).
        /// Set to true when syncing from FlowView to prevent event loops.
        /// </summary>
        public static bool SuppressCollapseEvents { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public TreeViewSafeNode(DialogNode originalNode, HashSet<DialogNode>? ancestors = null, int depth = 0, DialogPtr? sourcePointer = null, bool isUnreachableSibling = false)
        {
            _originalNode = originalNode;
            _sourcePointer = sourcePointer;
            _ancestorNodes = ancestors ?? new HashSet<DialogNode>();
            _depth = depth;
            _isUnreachableSibling = isUnreachableSibling;

            // Notify WPF that properties are available
            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(Speaker));
            OnPropertyChanged(nameof(TypeDisplay));
        }

        // Expose the source pointer for properties panel
        public DialogPtr? SourcePointer => _sourcePointer;

        // Check if this node is a child/link (IsChild=1)
        public bool IsChild => _sourcePointer?.IsLink ?? false;

        /// <summary>
        /// Issue #484: True if this is an NPC entry sibling that will never be reached
        /// because a prior sibling has no condition script (Aurora picks first passing condition).
        /// </summary>
        public bool IsUnreachableSibling => _isUnreachableSibling;

        // IsExpanded for TreeView expand/collapse functionality
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));

                    // LAZY LOADING FIX (Issue #82): Populate children when node is expanded
                    if (_isExpanded)
                    {
                        try
                        {
                            // Ensure _children collection exists
                            if (_children == null)
                            {
                                _children = new ObservableCollection<TreeViewSafeNode>();
                            }

                            // Remove placeholder if present
                            var placeholder = _children.OfType<TreeViewPlaceholderNode>().FirstOrDefault();
                            if (placeholder != null)
                            {
                                _children.Remove(placeholder);
                            }

                            // Populate if empty (after removing placeholder)
                            if (_children.Count == 0)
                            {
                                PopulateChildrenInternal();
                            }
                        }
                        catch (Exception ex)
                        {
                            DialogEditor.Services.UnifiedLogger.LogApplication(DialogEditor.Services.LogLevel.ERROR,
                                $"🌳 TreeView: Exception during lazy load of '{_originalNode?.DisplayText ?? "null"}': {ex.Message}");
                            // Don't re-throw - allow UI to continue functioning
                        }
                    }

                    // Publish collapse/expand event for FlowView sync (#451)
                    // Only publish if not a link node and not suppressed (prevents loops)
                    if (!SuppressCollapseEvents && !IsChild && _originalNode != null)
                    {
                        if (_isExpanded)
                        {
                            DialogEditor.Services.DialogChangeEventBus.Instance.PublishNodeExpanded(_originalNode, "TreeView");
                        }
                        else
                        {
                            DialogEditor.Services.DialogChangeEventBus.Instance.PublishNodeCollapsed(_originalNode, "TreeView");
                        }
                    }
                }
            }
        }

        // Reset global tracking (call when loading a new dialog)
        public static void ResetGlobalTracking()
        {
            _globalExpandedNodes.Clear();
        }
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        // Expose original node properties for binding (virtual for override)
        public virtual string DisplayText
        {
            get
            {
                if (_originalNode == null) return "[CONTINUE]";

                // Issue #353: Empty terminal nodes show [END DIALOG] instead of [CONTINUE]
                string text;
                if (string.IsNullOrEmpty(_originalNode.DisplayText))
                {
                    // Check if this is a terminal node (no children)
                    bool isTerminal = _originalNode.Pointers == null ||
                                      !_originalNode.Pointers.Any(p => p.Node != null);
                    text = isTerminal ? "[END DIALOG]" : "[CONTINUE]";
                }
                else
                {
                    text = _originalNode.DisplayText;
                }

                // Add speaker tag prefix for clarity
                if (_originalNode.Type == DialogNodeType.Entry)
                {
                    // Entry nodes (NPC)
                    var speaker = !string.IsNullOrEmpty(_originalNode.Speaker) ? _originalNode.Speaker : "Owner";
                    return $"[{speaker}] {text}";
                }
                else
                {
                    // Reply nodes - NPC if speaker set, PC if not
                    if (!string.IsNullOrEmpty(_originalNode.Speaker))
                    {
                        return $"[{_originalNode.Speaker}] {text}";
                    }
                    else
                    {
                        return $"[PC] {text}";
                    }
                }
            }
        }
        public virtual string Speaker =>
            !string.IsNullOrEmpty(_originalNode?.Speaker) ? _originalNode.Speaker :
            (_originalNode?.Type == DialogNodeType.Entry ? "Owner" : "PC");
        public virtual string TypeDisplay => _originalNode?.Type == DialogNodeType.Entry ? "Owner" : "PC";

        // Node color for tree view display
        public virtual string NodeColor
        {
            get
            {
                // Child/Link nodes are gray (matching NWN Toolset)
                if (IsChild) return "Gray";

                bool isPC = _originalNode?.Type == DialogNodeType.Reply && string.IsNullOrEmpty(_originalNode.Speaker);
                string speaker = _originalNode?.Speaker ?? "";

                return Utils.SpeakerVisualHelper.GetSpeakerColor(speaker, isPC);
            }
        }

        // Node shape for tree view display
        public virtual string NodeShapeGeometry
        {
            get
            {
                // Child/Link nodes get circle (default)
                if (IsChild)
                    return Utils.SpeakerVisualHelper.GetShapeGeometry(Utils.SpeakerVisualHelper.SpeakerShape.Circle);

                bool isPC = _originalNode?.Type == DialogNodeType.Reply && string.IsNullOrEmpty(_originalNode.Speaker);
                string speaker = _originalNode?.Speaker ?? "";

                var shape = Utils.SpeakerVisualHelper.GetSpeakerShape(speaker, isPC);
                return Utils.SpeakerVisualHelper.GetShapeGeometry(shape);
            }
        }

        // Formatted display text that matches copy tree structure format
        public virtual string FormattedDisplayText
        {
            get
            {
                if (_originalNode == null) return "[CONTINUE]";

                string speaker;
                if (_originalNode.Type == DialogNodeType.Reply)
                {
                    speaker = "[PC]";
                }
                else
                {
                    // For Entry nodes (NPC dialog), honor the Speaker tag if present
                    if (!string.IsNullOrEmpty(_originalNode.Speaker))
                    {
                        speaker = $"[{_originalNode.Speaker}]";
                    }
                    else
                    {
                        speaker = "[Owner]";
                    }
                }

                // Issue #353: Empty terminal nodes show [END DIALOG] instead of [CONTINUE]
                string text;
                if (string.IsNullOrEmpty(_originalNode.DisplayText))
                {
                    bool isTerminal = _originalNode.Pointers == null ||
                                      !_originalNode.Pointers.Any(p => p.Node != null);
                    text = isTerminal ? "[END DIALOG]" : "[CONTINUE]";
                }
                else
                {
                    text = _originalNode.DisplayText;
                }

                return $"{speaker} \"{text}\"";
            }
        }
        
        // Access to original node for tree structure operations
        public DialogNode OriginalNode => _originalNode;
        
        // Lazy-loaded children with circular reference protection (virtual for override)
        public virtual ObservableCollection<TreeViewSafeNode>? Children
        {
            get
            {
                // Child/link nodes are terminal - return null to prevent WPF from showing expand arrow (NWN Toolset behavior)
                if (IsChild)
                {
                    return null;
                }

                if (_children == null)
                {
                    _children = new ObservableCollection<TreeViewSafeNode>();

                    // LAZY LOADING FIX (Issue #82): Don't auto-populate children
                    // Children are populated on-demand when user expands the node
                    // This eliminates exponential memory/CPU usage at deep tree levels

                    // Add placeholder to show expander chevron if node has potential children
                    if (HasChildren)
                    {
                        // Empty placeholder - will be replaced when node is expanded
                        _children.Add(new TreeViewPlaceholderNode());
                    }
                }
                return _children;
            }
        }

        
        // Populate children on demand (called when user expands the node or tree search)
        public void PopulateChildren()
        {
            // Ensure _children collection exists (may be null if Children getter never accessed)
            if (_children == null)
            {
                _children = new ObservableCollection<TreeViewSafeNode>();
            }

            // Remove placeholder if present
            var placeholder = _children.OfType<TreeViewPlaceholderNode>().FirstOrDefault();
            if (placeholder != null)
            {
                _children.Remove(placeholder);
            }

            // Only populate if empty (after removing placeholder)
            if (_children.Count == 0)
            {
                PopulateChildrenInternal();
            }
        }
        
        // Internal method to actually populate the children
        // Matches copy tree function logic exactly for consistency
        private void PopulateChildrenInternal()
        {
            if (_children == null || _children.Count > 0)
                return; // Already populated or not initialized

            // Null safety check for _originalNode (#375 stability)
            if (_originalNode == null)
            {
                DialogEditor.Services.UnifiedLogger.LogApplication(DialogEditor.Services.LogLevel.WARN, "🌳 TreeView: _originalNode is null in PopulateChildrenInternal");
                return;
            }

            // Add basic depth protection to prevent infinite recursion (same as copy tree)
            // Issue #32: Increased from 50 to 250 to support dialogs up to depth 100+
            // (Each Entry→Reply pair counts as 2 depth levels in TreeView)
            if (_depth >= 250)
            {
                DialogEditor.Services.UnifiedLogger.LogApplication(DialogEditor.Services.LogLevel.WARN, $"🌳 TreeView: Hit max depth {_depth} for node '{_originalNode.DisplayText}' - dialog may be extremely deep");
                return;
            }

            // Null safety check for Pointers (#375 stability)
            if (_originalNode.Pointers == null)
            {
                DialogEditor.Services.UnifiedLogger.LogApplication(DialogEditor.Services.LogLevel.DEBUG, $"🌳 TreeView: Node '{_originalNode.DisplayText}' has null Pointers");
                return;
            }

            DialogEditor.Services.UnifiedLogger.LogApplication(DialogEditor.Services.LogLevel.DEBUG, $"🌳 TreeView: Populating children for '{_originalNode.DisplayText}' at depth {_depth}, has {_originalNode.Pointers.Count} pointers");

            // Track nodes already added to this parent to prevent duplicates (same as copy tree)
            var addedNodes = new HashSet<DialogNode>();

            // Issue #484: Pre-calculate unreachable sibling warnings for NPC entries
            // Aurora picks the first entry whose condition passes. If an entry has no condition,
            // it always passes, making all subsequent sibling entries unreachable.
            var unreachableSiblingIndices = CalculateUnreachableSiblings(_originalNode.Pointers);

            int pointerIndex = 0;
            foreach (var pointer in _originalNode.Pointers)
            {
                if (pointer.Node != null)
                {
                    DialogEditor.Services.UnifiedLogger.LogApplication(DialogEditor.Services.LogLevel.DEBUG, $"🌳 TreeView: Processing pointer to '{pointer.Node.DisplayText}' (Type: {pointer.Type}, Index: {pointer.Index}, IsLink: {pointer.IsLink})");

                    // Skip if we already added this node to this parent (same as copy tree)
                    if (addedNodes.Contains(pointer.Node))
                    {
                        DialogEditor.Services.UnifiedLogger.LogApplication(DialogEditor.Services.LogLevel.DEBUG, $"🌳 TreeView: Skipping duplicate node '{pointer.Node.DisplayText}'");
                        continue;
                    }

                    // Track that we're adding this node
                    addedNodes.Add(pointer.Node);

                    // Links are ALWAYS shown as link nodes (gray, IsChild marked) - don't expand them
                    if (pointer.IsLink)
                    {
                        DialogEditor.Services.UnifiedLogger.LogApplication(DialogEditor.Services.LogLevel.DEBUG, $"🌳 TreeView: Creating link node for '{pointer.Node.DisplayText}' (IsLink=true)");
                        var linkNode = new TreeViewSafeLinkNode(pointer.Node, _depth + 1, "Link", pointer, _originalNode);
                        _children.Add(linkNode);
                    }
                    // Check if node is in our ancestor chain (circular reference within this path)
                    else if (_ancestorNodes.Contains(pointer.Node))
                    {
                        DialogEditor.Services.UnifiedLogger.LogApplication(DialogEditor.Services.LogLevel.DEBUG, $"🌳 TreeView: Creating circular link for '{pointer.Node.DisplayText}' (ancestor chain protection)");
                        var linkNode = new TreeViewSafeLinkNode(pointer.Node, _depth + 1, "Circular", null, _originalNode);
                        _children.Add(linkNode);
                    }
                    else
                    {
                        // This is a real child node - expand it with updated ancestor chain
                        DialogEditor.Services.UnifiedLogger.LogApplication(DialogEditor.Services.LogLevel.DEBUG, $"🌳 TreeView: Creating full child node for '{pointer.Node.DisplayText}'");

                        // Pass down ancestor chain for circular detection AND the source pointer for properties display
                        var newAncestors = new HashSet<DialogNode>(_ancestorNodes) { _originalNode };

                        // Issue #484: Check if this entry is unreachable
                        bool isUnreachable = unreachableSiblingIndices.Contains(pointerIndex);

                        var childNode = new TreeViewSafeNode(pointer.Node, newAncestors, _depth + 1, pointer, isUnreachable);
                        _children.Add(childNode);
                    }
                }
                else
                {
                    DialogEditor.Services.UnifiedLogger.LogApplication(DialogEditor.Services.LogLevel.WARN, $"🌳 TreeView: Pointer to Index {pointer.Index} has null Node!");
                }
                pointerIndex++;
            }

            DialogEditor.Services.UnifiedLogger.LogApplication(DialogEditor.Services.LogLevel.DEBUG, $"🌳 TreeView: Finished populating '{_originalNode.DisplayText}', added {_children.Count} children");
        }

        /// <summary>
        /// Issue #484: Calculate which entry siblings are unreachable.
        /// Aurora engine picks the first NPC entry whose condition passes.
        /// If an entry has no condition script, it always passes, blocking all subsequent siblings.
        /// </summary>
        /// <param name="pointers">List of sibling pointers to analyze</param>
        /// <returns>Set of pointer indices that are unreachable</returns>
        public static HashSet<int> CalculateUnreachableSiblings(IList<DialogPtr> pointers)
        {
            var unreachableIndices = new HashSet<int>();

            // Only applies to Entry type siblings (NPC responses)
            // Reply siblings (PC choices) are all shown to the player
            var entryPointers = pointers
                .Select((ptr, idx) => (ptr, idx))
                .Where(x => x.ptr.Type == DialogNodeType.Entry && !x.ptr.IsLink && x.ptr.Node != null)
                .ToList();

            if (entryPointers.Count <= 1)
                return unreachableIndices; // No siblings to compare

            // Find the first entry without a condition - it blocks everything after it
            bool foundBlocker = false;
            foreach (var (ptr, idx) in entryPointers)
            {
                if (foundBlocker)
                {
                    // Everything after an unconditional entry is unreachable
                    unreachableIndices.Add(idx);
                }
                else if (string.IsNullOrEmpty(ptr.ScriptAppears))
                {
                    // This entry has no condition - it will always be picked
                    // All subsequent siblings become unreachable
                    foundBlocker = true;
                }
            }

            return unreachableIndices;
        }

        // For TreeView binding - determines if node should show expand arrow (virtual for override)
        // Child/link nodes don't expand, matching NWN Toolset behavior
        public virtual bool HasChildren
        {
            get
            {
                // Link nodes are terminal - no expand arrow
                if (IsChild) return false;

                // Check if underlying dialog node has any pointers
                if (_originalNode?.Pointers == null) return false;

                return _originalNode.Pointers.Any(p => p.Node != null);
            }
        }

        // Helper method to determine if a node has multiple parents in the conversation tree
        private bool HasMultipleParents(DialogNode node)
        {
            // This would require scanning the entire dialog structure
            // For now, let's disable the global expansion tracking logic entirely
            // and rely only on ancestor chain circular reference detection
            return false;
        }
    }
    
    /// <summary>
    /// A special node that represents a link to prevent circular expansion and shared content duplication
    /// Shows a preview of the linked conversation content in a greyed-out style
    /// </summary>
    public class TreeViewSafeLinkNode : TreeViewSafeNode
    {
        private readonly DialogNode _linkedNode;
        private readonly string _linkType;
        private readonly DialogNode? _parentNode;  // Parent node for link ID construction (#234)

        public TreeViewSafeLinkNode(DialogNode linkedNode, int depth, string linkType = "Link", DialogPtr? sourcePointer = null, DialogNode? parentNode = null)
            : base(linkedNode, new HashSet<DialogNode>(), depth, sourcePointer)
        {
            _linkedNode = linkedNode;
            _linkType = linkType;
            _parentNode = parentNode;
        }

        /// <summary>
        /// The parent node that contains the pointer to this link (for link ID construction #234)
        /// </summary>
        public DialogNode? ParentNode => _parentNode;

        // DisplayText now uses base class implementation which properly handles:
        // - Empty text showing as [CONTINUE]
        // - [PC] or [Owner/Speaker] prefixes
        // This fixes the empty link display bug
        public override string TypeDisplay => $"Link ({base.TypeDisplay})";

        // Links are terminal - no children shown (NWN Toolset behavior)
        public override ObservableCollection<TreeViewSafeNode>? Children
        {
            get
            {
                // Links don't show children - return null to hide expand arrow
                return null;
            }
        }

        public override bool HasChildren => false; // Links don't expand
    }

    /// <summary>
    /// A greyed-out preview node that shows linked content without allowing further expansion
    /// </summary>
    public class TreeViewSafePreviewNode : TreeViewSafeNode
    {
        private readonly string? _customText;

        public TreeViewSafePreviewNode(DialogNode? previewNode, int depth, string? customText = null)
            : base(previewNode ?? new DialogNode(), new HashSet<DialogNode>(), depth)
        {
            _customText = customText;
        }

        public override string DisplayText => _customText ?? base.DisplayText;
        public override string TypeDisplay => $"Preview ({base.TypeDisplay})";
        public override ObservableCollection<TreeViewSafeNode>? Children => null; // No children for previews
        public override bool HasChildren => false;
    }

    /// <summary>
    /// Special root node that represents the dialog file itself (matches NWN Toolset)
    /// </summary>
    public class TreeViewRootNode : TreeViewSafeNode
    {
        private readonly Dialog _dialog;

        public TreeViewRootNode(Dialog dialog)
            : base(new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() })
        {
            _dialog = dialog;
            // NOTE: Don't set IsExpanded here - PopulateDialogNodes will set it after adding children
        }

        public override string DisplayText => "ROOT";
        public override string NodeColor => "Gray";
        public bool IsRoot => true;
        public Dialog Dialog => _dialog;

        /// <summary>
        /// Override HasChildren to check actual Children collection instead of pointers.
        /// ROOT node's children are added directly by PopulateDialogNodes, not via pointers.
        /// </summary>
        public override bool HasChildren => Children != null && Children.Count > 0;
    }

    /// <summary>
    /// Placeholder node to show expander chevron for lazy-loaded children
    /// </summary>
    public class TreeViewPlaceholderNode : TreeViewSafeNode
    {
        public TreeViewPlaceholderNode()
            : base(new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() })
        {
        }

        public override string DisplayText => "Loading...";
        public override string NodeColor => "Gray";
        public override ObservableCollection<TreeViewSafeNode>? Children => null;
        public override bool HasChildren => false;
    }
}