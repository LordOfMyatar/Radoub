using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace MerchantEditor.Services;

/// <summary>
/// Settings service for Fence.
/// Stores tool-specific settings in ~/Radoub/Fence/FenceSettings.json
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

    protected override string ToolName => "Fence";
    protected override string SettingsEnvironmentVariable => "FENCE_SETTINGS_DIR";
    protected override string SettingsFileName => "FenceSettings.json";

    // Panel settings
    private double _leftPanelWidth = 450;
    private double _rightPanelWidth = 400;

    // Store browser panel settings (#1144)
    private double _storeBrowserPanelWidth = 200;
    private bool _storeBrowserPanelVisible = true;

    // Item details panel settings (#1259)
    private double _itemDetailsPanelWidth = 250;
    private bool _itemDetailsPanelVisible = true;

    private SettingsService()
    {
        Initialize();
    }

    // Panel properties
    public double LeftPanelWidth
    {
        get => _leftPanelWidth;
        set { if (SetProperty(ref _leftPanelWidth, Math.Max(250, Math.Min(700, value)))) SaveSettings(); }
    }

    public double RightPanelWidth
    {
        get => _rightPanelWidth;
        set { if (SetProperty(ref _rightPanelWidth, Math.Max(250, Math.Min(700, value)))) SaveSettings(); }
    }

    // Store browser panel properties (#1144)
    public double StoreBrowserPanelWidth
    {
        get => _storeBrowserPanelWidth;
        set { if (SetProperty(ref _storeBrowserPanelWidth, Math.Max(150, Math.Min(400, value)))) SaveSettings(); }
    }

    public bool StoreBrowserPanelVisible
    {
        get => _storeBrowserPanelVisible;
        set { if (SetProperty(ref _storeBrowserPanelVisible, value)) SaveSettings(); }
    }

    // Item details panel properties (#1259)
    public double ItemDetailsPanelWidth
    {
        get => _itemDetailsPanelWidth;
        set { if (SetProperty(ref _itemDetailsPanelWidth, Math.Max(180, Math.Min(500, value)))) SaveSettings(); }
    }

    public bool ItemDetailsPanelVisible
    {
        get => _itemDetailsPanelVisible;
        set { if (SetProperty(ref _itemDetailsPanelVisible, value)) SaveSettings(); }
    }

    /// <summary>
    /// Validate recent files asynchronously and remove missing ones.
    /// Call this when populating the recent files menu to avoid blocking on network paths.
    /// </summary>
    public async System.Threading.Tasks.Task ValidateRecentFilesAsync()
    {
        var currentFiles = RecentFiles;
        var missingFiles = await System.Threading.Tasks.Task.Run(() =>
        {
            return currentFiles.Where(f => !File.Exists(f)).ToList();
        });

        foreach (var file in missingFiles)
        {
            RemoveRecentFile(file);
        }

        if (missingFiles.Count > 0)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Removed {missingFiles.Count} missing files from recent files list");
        }
    }

    /// <summary>
    /// Fence defers file validation to avoid blocking on network paths during startup.
    /// </summary>
    protected override void ValidateRecentFilesOnLoad()
    {
        // Intentionally empty - validation deferred to ValidateRecentFilesAsync()
    }

    protected override void LoadToolSettings(SettingsData settings)
    {
        _leftPanelWidth = Math.Max(250, Math.Min(700, settings.LeftPanelWidth));
        _rightPanelWidth = Math.Max(250, Math.Min(700, settings.RightPanelWidth));
        _storeBrowserPanelWidth = Math.Max(150, Math.Min(400, settings.StoreBrowserPanelWidth));
        _storeBrowserPanelVisible = settings.StoreBrowserPanelVisible;
        _itemDetailsPanelWidth = Math.Max(180, Math.Min(500, settings.ItemDetailsPanelWidth));
        _itemDetailsPanelVisible = settings.ItemDetailsPanelVisible;
    }

    protected override void SaveToolSettings(SettingsData settings)
    {
        settings.LeftPanelWidth = LeftPanelWidth;
        settings.RightPanelWidth = RightPanelWidth;
        settings.StoreBrowserPanelWidth = StoreBrowserPanelWidth;
        settings.StoreBrowserPanelVisible = StoreBrowserPanelVisible;
        settings.ItemDetailsPanelWidth = ItemDetailsPanelWidth;
        settings.ItemDetailsPanelVisible = ItemDetailsPanelVisible;
    }

    public class SettingsData : BaseSettingsData
    {
        public double LeftPanelWidth { get; set; } = 450;
        public double RightPanelWidth { get; set; } = 400;

        // Store browser panel (#1144)
        public double StoreBrowserPanelWidth { get; set; } = 200;
        public bool StoreBrowserPanelVisible { get; set; } = true;

        // Item details panel (#1259)
        public double ItemDetailsPanelWidth { get; set; } = 250;
        public bool ItemDetailsPanelVisible { get; set; } = true;
    }
}
