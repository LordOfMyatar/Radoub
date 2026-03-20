using System;
using System.Collections.Generic;
using System.ComponentModel;
using DialogEditor.Utils;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace DialogEditor.Services
{
    /// <summary>
    /// Interface for Parley application settings.
    /// Provides access to user preferences, game paths, and configuration.
    /// #1230: Phase 3 - Service interface extraction for dependency injection.
    /// #1447: Extends IWindowSettings for WindowPositionHelper compatibility.
    /// </summary>
    public interface ISettingsService : INotifyPropertyChanged, IWindowSettings
    {
        // Recent files (delegated to RecentFilesService)
        List<string> RecentFiles { get; }
        int MaxRecentFiles { get; set; }
        void AddRecentFile(string filePath);
        void RemoveRecentFile(string filePath);
        void ClearRecentFiles();
        void CleanupRecentFiles();

        // Panel layout (delegated to WindowLayoutService)
        double LeftPanelWidth { get; set; }
        double TopLeftPanelHeight { get; set; }

        // UI settings (delegated to UISettingsService)
        double FontSize { get; set; }
        string FontFamily { get; set; }
        bool IsDarkTheme { get; set; }
        string CurrentThemeId { get; set; }
        bool UseSharedTheme { get; set; }
        string FlowchartLayout { get; set; }
        bool AllowScrollbarAutoHide { get; set; }
        int FlowchartNodeMaxLines { get; set; }
        bool TreeViewWordWrap { get; set; }

        // Flowchart window (delegated to WindowLayoutService)
        double FlowchartWindowLeft { get; set; }
        double FlowchartWindowTop { get; set; }
        double FlowchartWindowWidth { get; set; }
        double FlowchartWindowHeight { get; set; }
        bool FlowchartWindowOpen { get; set; }
        double FlowchartPanelWidth { get; set; }
        bool FlowchartVisible { get; set; }

        // Dialog browser panel (delegated to WindowLayoutService)
        double DialogBrowserPanelWidth { get; set; }
        bool DialogBrowserPanelVisible { get; set; }

        // Game settings (delegated to shared RadoubSettings)
        string NeverwinterNightsPath { get; set; }
        string BaseGameInstallPath { get; set; }
        string CurrentModulePath { get; set; }
        string TlkLanguage { get; set; }
        bool TlkUseFemale { get; set; }

        // Module paths
        List<string> ModulePaths { get; }
        void AddModulePath(string path);
        void RemoveModulePath(string path);
        void ClearModulePaths();

        // Logging settings
        int LogRetentionSessions { get; set; }
        LogLevel CurrentLogLevel { get; set; }
        LogLevel DebugLogFilterLevel { get; set; }
        bool DebugWindowVisible { get; set; }

        // Auto-save settings
        bool AutoSaveEnabled { get; set; }
        int AutoSaveDelayMs { get; set; }
        int AutoSaveIntervalMinutes { get; set; }
        int EffectiveAutoSaveIntervalMs { get; }

        // NPC speaker preferences
        Dictionary<string, SpeakerPreferences> NpcSpeakerPreferences { get; }
        void SetSpeakerPreference(string speakerTag, string? color, SpeakerVisualHelper.SpeakerShape? shape);
        (string? color, SpeakerVisualHelper.SpeakerShape? shape) GetSpeakerPreference(string speakerTag);
        bool EnableNpcTagColoring { get; set; }

        // Confirmation dialogs
        bool ShowDeleteConfirmation { get; set; }

        // Conversation Simulator
        bool SimulatorShowWarnings { get; set; }

        // Script editor settings
        string ExternalEditorPath { get; set; }
        List<string> ScriptSearchPaths { get; set; }

        // Radoub tool integration
        string ManifestPath { get; set; }

        // Parameter cache settings
        bool EnableParameterCache { get; set; }
        int MaxCachedValuesPerParameter { get; set; }
        int MaxCachedScripts { get; set; }

        // Sound Browser settings
        bool SoundBrowserIncludeGameResources { get; set; }
        bool SoundBrowserIncludeHakFiles { get; set; }
        bool SoundBrowserIncludeBifFiles { get; set; }

        // Spell Check settings
        bool SpellCheckEnabled { get; set; }

        // Recent creature tags for character picker (#1244)
        List<string> RecentCreatureTags { get; }
        void SetRecentCreatureTags(List<string> tags);
    }
}
