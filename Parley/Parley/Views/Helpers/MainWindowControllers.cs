namespace Parley.Views.Helpers
{
    /// <summary>
    /// Container for MainWindow's UI controller dependencies.
    /// Controllers coordinate complex UI interactions.
    /// </summary>
    public class MainWindowControllers
    {
        public FlowchartManager Flowchart { get; set; } = null!;
        public TreeViewUIController TreeView { get; set; } = null!;
        public ScriptBrowserController ScriptBrowser { get; set; } = null!;
        public QuestUIController Quest { get; set; } = null!;
        public FileMenuController FileMenu { get; set; } = null!;
        public EditMenuController EditMenu { get; set; } = null!;
    }
}
