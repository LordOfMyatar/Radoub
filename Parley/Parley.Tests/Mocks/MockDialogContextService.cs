using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Tests.Mocks
{
    /// <summary>
    /// Mock IDialogContextService for unit testing.
    /// Allows test setup of current dialog state.
    /// </summary>
    public class MockDialogContextService : IDialogContextService
    {
        public Dialog? CurrentDialog { get; set; }
        public string? CurrentFileName { get; set; }
        public string? CurrentFilePath { get; set; }

        public event EventHandler? DialogChanged;

        private List<DialogNodeInfo> _nodes = new();
        private List<DialogLinkInfo> _links = new();

        public void NotifyDialogChanged()
        {
            DialogChanged?.Invoke(this, EventArgs.Empty);
        }

        public (List<DialogNodeInfo> Nodes, List<DialogLinkInfo> Links) GetDialogStructure()
        {
            return (_nodes, _links);
        }

        /// <summary>
        /// Set up dialog structure for testing flowchart/visualization code.
        /// </summary>
        public void SetupDialogStructure(List<DialogNodeInfo> nodes, List<DialogLinkInfo> links)
        {
            _nodes = nodes;
            _links = links;
        }
    }
}
