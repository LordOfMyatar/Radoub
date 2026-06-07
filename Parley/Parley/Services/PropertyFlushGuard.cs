using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Guards the property-panel "flush to model" step against cross-node corruption (#2382).
    ///
    /// The property TextBoxes are populated FROM one DialogNode but flushed back on selection
    /// change / file save. If selection moves to a different node (e.g. a drag-drop refresh
    /// restores selection to a sibling) without the panel being repopulated, a flush would
    /// write the displayed node's text onto the newly-selected node. This guard refuses to
    /// flush unless the panel's source node is the same instance as the current selection.
    /// </summary>
    public static class PropertyFlushGuard
    {
        /// <summary>
        /// True only when the panel may safely flush: a node is selected and the panel was
        /// last populated from that exact same DialogNode instance.
        /// </summary>
        public static bool ShouldFlush(DialogNode? selectedNode, DialogNode? lastPopulatedNode)
        {
            if (selectedNode == null || lastPopulatedNode == null)
                return false;

            return ReferenceEquals(selectedNode, lastPopulatedNode);
        }
    }
}
