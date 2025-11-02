using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DialogEditor.Models
{
    public class TreeSafeDialogNode
    {
        private static readonly HashSet<DialogNode> _visitedNodes = new HashSet<DialogNode>();
        private const int MaxDepth = 10;
        
        public DialogNode OriginalNode { get; }
        public string TypeDisplay => OriginalNode.TypeDisplay;
        public string DisplayText => OriginalNode.DisplayText ?? "";
        public string Speaker => OriginalNode.Speaker ?? "";
        public ObservableCollection<TreeSafeDialogPtr> Pointers { get; }
        
        public TreeSafeDialogNode(DialogNode node, int depth = 0)
        {
            OriginalNode = node;
            Pointers = new ObservableCollection<TreeSafeDialogPtr>();
            
            // Prevent infinite loops by checking if we've visited this node before
            // or if we've exceeded maximum depth
            if (_visitedNodes.Contains(node) || depth >= MaxDepth)
            {
                // Don't populate pointers to prevent infinite recursion
                return;
            }
            
            _visitedNodes.Add(node);
            
            foreach (var pointer in node.Pointers)
            {
                Pointers.Add(new TreeSafeDialogPtr(pointer, depth + 1));
            }
            
            _visitedNodes.Remove(node);
        }
        
        public static void ResetVisitedNodes()
        {
            _visitedNodes.Clear();
        }
    }
    
    public class TreeSafeDialogPtr
    {
        public DialogPtr OriginalPointer { get; }
        public TreeSafeDialogNode? Node { get; private set; }
        public uint Index => OriginalPointer.Index;
        public DialogNodeType Type => OriginalPointer.Type;
        public bool IsLink => OriginalPointer.IsLink;
        
        public TreeSafeDialogPtr(DialogPtr pointer, int depth)
        {
            OriginalPointer = pointer;
            Node = pointer.Node != null ? new TreeSafeDialogNode(pointer.Node, depth) : null;
        }
    }
}