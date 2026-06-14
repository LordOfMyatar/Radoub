using System;
using Radoub.UI.Services;

namespace Manifest.Services
{
    /// <summary>
    /// Settings service for Manifest.
    /// Stores tool-specific settings in ~/Radoub/Manifest/ManifestSettings.json
    /// Game paths and TLK settings are in shared RadoubSettings.
    /// </summary>
    public class SettingsService : BaseToolSettingsService<SettingsService.SettingsData>
    {
        private static SettingsService? _instance;
        private static readonly object _lock = new();

        public static SettingsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SettingsService();
                    }
                }
                return _instance;
            }
        }

        protected override string ToolName => "Manifest";
        protected override string SettingsEnvironmentVariable => "MANIFEST_SETTINGS_DIR";
        protected override string SettingsFileName => "ManifestSettings.json";
        protected override double DefaultWindowWidth => 1000;
        protected override double DefaultWindowHeight => 700;
        protected override double MinWindowWidth => 400;
        protected override double MinWindowHeight => 300;

        // Panel settings
        private double _treePanelWidth = 300;

        private SettingsService()
        {
            Initialize();
        }

        // Panel properties
        public double TreePanelWidth
        {
            get => _treePanelWidth;
            set { if (SetProperty(ref _treePanelWidth, Math.Max(150, Math.Min(600, value)))) SaveSettings(); }
        }

        // SpellCheckEnabled is provided by BaseToolSettingsService (#2390),
        // reading/writing the same "SpellCheckEnabled" JSON key for compatibility.

        protected override void LoadToolSettings(SettingsData settings)
        {
            _treePanelWidth = Math.Max(150, Math.Min(600, settings.TreePanelWidth));
        }

        protected override void SaveToolSettings(SettingsData settings)
        {
            settings.TreePanelWidth = TreePanelWidth;
        }

        public class SettingsData : BaseSettingsData
        {
            public double TreePanelWidth { get; set; } = 300;
        }
    }
}
