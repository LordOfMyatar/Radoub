using DialogEditor.Services;
using Parley.Services;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Container for MainWindow's service dependencies.
    /// Reduces field count and provides organized access to services.
    /// </summary>
    public class MainWindowServices
    {
        // Core services
        public AudioService Audio { get; }
        public CreatureService Creature { get; }

        // Property services
        public PropertyPanelPopulator PropertyPopulator { get; set; } = null!;
        public PropertyAutoSaveService PropertyAutoSave { get; set; } = null!;
        public ScriptParameterUIManager ParameterUI { get; set; } = null!;
        public ScriptPreviewService ScriptPreview { get; set; } = null!;

        // UI helpers
        public NodeCreationHelper NodeCreation { get; set; } = null!;
        public ResourceBrowserManager ResourceBrowser { get; set; } = null!;
        public KeyboardShortcutManager KeyboardShortcuts { get; }

        // Window services
        public DebugAndLoggingHandler DebugLogging { get; set; } = null!;
        public WindowPersistenceManager WindowPersistence { get; set; } = null!;

        // TreeView and dialog services
        public TreeViewDragDropService DragDrop { get; }
        public DialogFactory Dialog { get; set; } = null!;

        public MainWindowServices()
        {
            // Services with no dependencies
            Audio = new AudioService();
            Creature = new CreatureService();
            KeyboardShortcuts = new KeyboardShortcutManager();
            DragDrop = new TreeViewDragDropService();
        }
    }
}
