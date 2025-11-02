using System;
using System.Collections.Generic;
using DialogEditor.Models;

namespace DumpChildLinks
{
    public static class TreeDump
    {
        public static void DumpTree(Dialog dialog)
        {
            Console.WriteLine("=== TREE STRUCTURE ===");
            Console.WriteLine();

            foreach (var start in dialog.Starts)
            {
                Console.WriteLine($"START -> Entry[{start.Index}]");
                DumpNode(start.Node, start, 1, new HashSet<DialogNode>());
                Console.WriteLine();
            }
        }

        private static void DumpNode(DialogNode node, DialogPtr? ptr, int depth, HashSet<DialogNode> visited)
        {
            string indent = new string(' ', depth * 2);
            string type = node.Type == DialogNodeType.Entry ? "Owner" : "PC";
            string linkMarker = ptr?.IsLink == true ? " [CHILD LINK]" : "";
            string linkComment = ptr?.IsLink == true ? $" LinkComment=\"{ptr.LinkComment}\"" : "";

            Console.WriteLine($"{indent}[{type}] \"{node.DisplayText}\"{linkMarker}{linkComment}");

            // Don't expand child links (NWN Toolset behavior)
            if (ptr?.IsLink == true)
            {
                Console.WriteLine($"{indent}  (terminal - no children shown)");
                return;
            }

            // Prevent infinite loops
            if (visited.Contains(node))
            {
                Console.WriteLine($"{indent}  (circular reference detected)");
                return;
            }

            visited.Add(node);

            foreach (var childPtr in node.Pointers)
            {
                if (childPtr.Node != null)
                {
                    DumpNode(childPtr.Node, childPtr, depth + 1, new HashSet<DialogNode>(visited));
                }
            }
        }
    }
}
