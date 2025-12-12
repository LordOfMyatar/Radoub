using System;
using Avalonia.Controls;
using DialogEditor.Models;

namespace DialogEditor.Views
{
    /// <summary>
    /// Window wrapper for FlowchartPanel.
    /// Used for "Floating Window" layout mode.
    /// </summary>
    public partial class FlowchartWindow : Window
    {
        /// <summary>
        /// Raised when a flowchart node is clicked.
        /// The FlowchartNode parameter contains the clicked node with context (IsLink, OriginalPointer, etc.)
        /// </summary>
        public event EventHandler<FlowchartNode?>? NodeClicked;

        public FlowchartWindow()
        {
            InitializeComponent();

            // Forward node click events from the panel
            FlowchartPanelControl.NodeClicked += (sender, node) => NodeClicked?.Invoke(this, node);
        }

        /// <summary>
        /// Update the flowchart to display the given dialog
        /// </summary>
        /// <param name="dialog">The dialog to display</param>
        /// <param name="fileName">Optional filename for display</param>
        public void UpdateDialog(Dialog? dialog, string? fileName = null)
        {
            FlowchartPanelControl.UpdateDialog(dialog, fileName);

            // Update window title with filename
            if (!string.IsNullOrEmpty(fileName))
            {
                Title = $"Flowchart - {System.IO.Path.GetFileName(fileName)}";
            }
            else
            {
                Title = "Flowchart View";
            }
        }

        /// <summary>
        /// Clear the flowchart display
        /// </summary>
        public void Clear()
        {
            FlowchartPanelControl.Clear();
            Title = "Flowchart View";
        }

        /// <summary>
        /// Select a node in the flowchart by DialogNode.
        /// Used for TreeView → Flowchart sync.
        /// </summary>
        public void SelectNode(DialogNode? dialogNode)
        {
            FlowchartPanelControl.SelectNode(dialogNode);
        }
    }
}
