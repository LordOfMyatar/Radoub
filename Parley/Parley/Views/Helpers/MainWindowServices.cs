using System;
using System.IO;
using DialogEditor.Services;
using Microsoft.Extensions.DependencyInjection;
using Parley.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Container for MainWindow's service dependencies.
    /// Reduces field count and provides organized access to services.
    /// #1232: Accepts IServiceProvider to resolve DI-registered services.
    /// </summary>
    public class MainWindowServices : IDisposable
    {
        // Core services
        public AudioService Audio { get; }
        public SoundPlaybackService SoundPlayback { get; }
        public CreatureService Creature { get; }

        // Game data services for BIF/TLK lookups (#916)
        public IGameDataService GameData { get; }
        public IImageService ImageService { get; }

        // DI-resolved services (#1232, #1233)
        public ISettingsService Settings { get; }
        public IDialogContextService DialogContext { get; }
        public IScriptService Script { get; }
        public IPortraitService Portrait { get; }
        public IJournalService Journal { get; }
        public UISettingsService UISettings { get; }

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

        public MainWindowServices(IServiceProvider serviceProvider)
        {
            // Resolve DI-registered services (#1232)
            Settings = serviceProvider.GetRequiredService<ISettingsService>();
            DialogContext = serviceProvider.GetRequiredService<IDialogContextService>();
            Script = serviceProvider.GetRequiredService<IScriptService>();
            Portrait = serviceProvider.GetRequiredService<IPortraitService>();
            Journal = serviceProvider.GetRequiredService<IJournalService>();
            UISettings = serviceProvider.GetRequiredService<UISettingsService>();

            // Services with no dependencies (not yet in DI container)
            Audio = new AudioService();
            SoundPlayback = new SoundPlaybackService(Audio, Settings);
            Creature = new CreatureService();
            KeyboardShortcuts = new KeyboardShortcutManager();
            DragDrop = new TreeViewDragDropService();

            // Game data services for portrait loading from BIF archives (#916)
            GameData = new GameDataService();
            if (GameData.IsConfigured)
            {
                // Configure module-aware HAK scanning for 2DA/resource resolution (#1314)
                var moduleDir = ResolveModuleWorkingDirectory();
                if (!string.IsNullOrEmpty(moduleDir))
                {
                    GameData.ConfigureModuleHaks(moduleDir);
                }
            }
            ImageService = new ImageService(GameData);
        }

        /// <summary>
        /// Resolve the module working directory from RadoubSettings.CurrentModulePath.
        /// Handles both .mod file paths (resolves to unpacked sibling directory) and direct directory paths.
        /// </summary>
        private static string? ResolveModuleWorkingDirectory()
        {
            var modulePath = RadoubSettings.Instance.CurrentModulePath;
            if (string.IsNullOrEmpty(modulePath))
                return null;

            // If it's a .mod file, look for the unpacked directory alongside it
            if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
            {
                var moduleName = Path.GetFileNameWithoutExtension(modulePath);
                var moduleDir = Path.GetDirectoryName(modulePath);
                if (!string.IsNullOrEmpty(moduleDir))
                {
                    var candidate = Path.Combine(moduleDir, moduleName);
                    if (Directory.Exists(candidate))
                        return candidate;
                }
            }

            // It's already a directory path
            if (Directory.Exists(modulePath))
                return modulePath;

            return null;
        }

        public void Dispose()
        {
            SoundPlayback?.Dispose();
            Audio?.Dispose();
            GameData?.Dispose();
        }
    }
}
