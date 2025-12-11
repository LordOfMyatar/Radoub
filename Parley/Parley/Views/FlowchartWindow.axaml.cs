using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.ViewModels;

namespace DialogEditor.Views
{
    /// <summary>
    /// Code-behind for the FlowchartWindow.
    /// Hosts the native flowchart visualization for dialog structure.
    /// </summary>
    public partial class FlowchartWindow : Window
    {
        private readonly FlowchartPanelViewModel _viewModel;

        public FlowchartWindow()
        {
            InitializeComponent();
            _viewModel = new FlowchartPanelViewModel();
            DataContext = _viewModel;
        }

        /// <summary>
        /// Update the flowchart to display the given dialog
        /// </summary>
        /// <param name="dialog">The dialog to display</param>
        /// <param name="fileName">Optional filename for display</param>
        public void UpdateDialog(Dialog? dialog, string? fileName = null)
        {
            _viewModel.UpdateDialog(dialog, fileName);

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
            _viewModel.Clear();
            Title = "Flowchart View";
        }
    }
}
