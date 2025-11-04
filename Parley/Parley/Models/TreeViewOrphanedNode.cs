using System.Collections.ObjectModel;
using DialogEditor.Models;

namespace Parley.Models
{
    /// <summary>
    /// Special tree node to contain orphaned dialog nodes
    /// These are nodes that exist in the dialog but aren't reachable from any START
    /// </summary>
    public class TreeViewOrphanedNode : TreeViewSafeNode
    {
        private readonly string _displayText;
        private ObservableCollection<TreeViewSafeNode> _orphanChildren;

        public TreeViewOrphanedNode(string displayText)
            : base(new DialogNode { Type = DialogNodeType.Entry, Text = new LocString() })
        {
            _displayText = displayText;
            _orphanChildren = new ObservableCollection<TreeViewSafeNode>();
            IsExpanded = false; // Start collapsed
        }

        public override string DisplayText => _displayText;

        // Override Children to return our custom collection
        public override ObservableCollection<TreeViewSafeNode>? Children => _orphanChildren;

        // Override HasChildren to indicate if we have orphans
        public override bool HasChildren => _orphanChildren?.Count > 0;

        public int OrphanCount => _orphanChildren?.Count ?? 0;

        public string StatusMessage => OrphanCount > 0
            ? $"⚠️ {OrphanCount} orphaned node{(OrphanCount > 1 ? "s" : "")} - may display differently in Aurora"
            : "";
    }
}