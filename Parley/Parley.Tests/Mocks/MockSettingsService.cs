using System.ComponentModel;
using DialogEditor.Services;
using DialogEditor.Utils;
using Radoub.Formats.Logging;

namespace Parley.Tests.Mocks
{
    /// <summary>
    /// Mock ISettingsService for unit testing.
    /// All properties have sensible test defaults.
    /// </summary>
    public class MockSettingsService : ISettingsService
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // Recent files
        public List<string> RecentFiles { get; } = new();
        public int MaxRecentFiles { get; set; } = 10;
        public void AddRecentFile(string filePath) => RecentFiles.Insert(0, filePath);
        public void RemoveRecentFile(string filePath) => RecentFiles.Remove(filePath);
        public void ClearRecentFiles() => RecentFiles.Clear();
        public void CleanupRecentFiles() { }

        // Window layout
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        public double WindowWidth { get; set; } = 1024;
        public double WindowHeight { get; set; } = 768;
        public bool WindowMaximized { get; set; } = false;

        // Panel layout
        public double LeftPanelWidth { get; set; } = 250;
        public double TopLeftPanelHeight { get; set; } = 300;

        // UI settings
        public double FontSize { get; set; } = 14;
        public string FontFamily { get; set; } = "Segoe UI";
        public bool IsDarkTheme { get; set; } = false;
        public string CurrentThemeId { get; set; } = "light";
        public string FlowchartLayout { get; set; } = "TopToBottom";
        public bool AllowScrollbarAutoHide { get; set; } = true;
        public int FlowchartNodeMaxLines { get; set; } = 3;
        public bool TreeViewWordWrap { get; set; } = false;

        // Flowchart window
        public double FlowchartWindowLeft { get; set; } = 200;
        public double FlowchartWindowTop { get; set; } = 200;
        public double FlowchartWindowWidth { get; set; } = 800;
        public double FlowchartWindowHeight { get; set; } = 600;
        public bool FlowchartWindowOpen { get; set; } = false;
        public double FlowchartPanelWidth { get; set; } = 400;
        public bool FlowchartVisible { get; set; } = false;

        // Dialog browser panel
        public double DialogBrowserPanelWidth { get; set; } = 300;
        public bool DialogBrowserPanelVisible { get; set; } = false;

        // Game settings
        public string NeverwinterNightsPath { get; set; } = "";
        public string BaseGameInstallPath { get; set; } = "";
        public string CurrentModulePath { get; set; } = "";
        public string TlkLanguage { get; set; } = "en";
        public bool TlkUseFemale { get; set; } = false;

        // Module paths
        public List<string> ModulePaths { get; } = new();
        public void AddModulePath(string path) => ModulePaths.Add(path);
        public void RemoveModulePath(string path) => ModulePaths.Remove(path);
        public void ClearModulePaths() => ModulePaths.Clear();

        // Logging settings
        public int LogRetentionSessions { get; set; } = 5;
        public LogLevel CurrentLogLevel { get; set; } = LogLevel.INFO;
        public LogLevel DebugLogFilterLevel { get; set; } = LogLevel.DEBUG;
        public bool DebugWindowVisible { get; set; } = false;

        // Auto-save settings
        public bool AutoSaveEnabled { get; set; } = false;
        public int AutoSaveDelayMs { get; set; } = 3000;
        public int AutoSaveIntervalMinutes { get; set; } = 5;
        public int EffectiveAutoSaveIntervalMs => AutoSaveIntervalMinutes * 60 * 1000;

        // NPC speaker preferences
        public Dictionary<string, SpeakerPreferences> NpcSpeakerPreferences { get; } = new();
        public bool EnableNpcTagColoring { get; set; } = false;

        public void SetSpeakerPreference(string speakerTag, string? color, SpeakerVisualHelper.SpeakerShape? shape)
        {
            NpcSpeakerPreferences[speakerTag] = new SpeakerPreferences
            {
                Color = color,
                Shape = shape?.ToString()
            };
        }

        public (string? color, SpeakerVisualHelper.SpeakerShape? shape) GetSpeakerPreference(string speakerTag)
        {
            if (NpcSpeakerPreferences.TryGetValue(speakerTag, out var prefs))
            {
                SpeakerVisualHelper.SpeakerShape? shape = null;
                if (Enum.TryParse<SpeakerVisualHelper.SpeakerShape>(prefs.Shape, out var parsed))
                    shape = parsed;
                return (prefs.Color, shape);
            }
            return (null, null);
        }

        // Confirmation dialogs
        public bool ShowDeleteConfirmation { get; set; } = true;

        // Conversation Simulator
        public bool SimulatorShowWarnings { get; set; } = true;

        // Script editor settings
        public string ExternalEditorPath { get; set; } = "";
        public List<string> ScriptSearchPaths { get; set; } = new();

        // Radoub tool integration
        public string ManifestPath { get; set; } = "";

        // Parameter cache settings
        public bool EnableParameterCache { get; set; } = true;
        public int MaxCachedValuesPerParameter { get; set; } = 100;
        public int MaxCachedScripts { get; set; } = 50;

        // Sound Browser settings
        public bool SoundBrowserIncludeGameResources { get; set; } = true;
        public bool SoundBrowserIncludeHakFiles { get; set; } = true;
        public bool SoundBrowserIncludeBifFiles { get; set; } = true;

        // Spell Check settings
        public bool SpellCheckEnabled { get; set; } = false;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
