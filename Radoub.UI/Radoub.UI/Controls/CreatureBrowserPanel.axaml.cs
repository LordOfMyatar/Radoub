using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace Radoub.UI.Controls;

/// <summary>
/// Creature-specific entry with BIC/UTC distinction.
/// </summary>
public class CreatureBrowserEntry : FileBrowserEntry
{
    /// <summary>
    /// True if this is a BIC file (player character), false for UTC (creature blueprint).
    /// </summary>
    public bool IsBic { get; set; }

    /// <summary>
    /// Type indicator for display.
    /// </summary>
    public string TypeIndicator => IsBic ? $"[BIC:{Source}]" : "[UTC:Module]";

    public override string DisplayName => IsBic ? $"{Name} {TypeIndicator}" : Name;
}

/// <summary>
/// Creature browser panel for embedding in Quartermaster's main window.
/// Provides .utc/.bic file list from module and vaults.
/// </summary>
public partial class CreatureBrowserPanel : FileBrowserPanelBase
{
    private readonly IScriptBrowserContext? _context;
    private List<CreatureBrowserEntry> _vaultEntries = new();

    public CreatureBrowserPanel() : this(null)
    {
    }

    public CreatureBrowserPanel(IScriptBrowserContext? context)
    {
        _context = context;
        InitializeComponent();

        FileExtension = ".utc";
        SearchWatermark = "Type to filter creatures...";

        // Wire up filter checkboxes
        if (ShowModuleCheck != null)
            ShowModuleCheck.IsCheckedChanged += OnFilterChanged;
        if (ShowLocalVaultCheck != null)
            ShowLocalVaultCheck.IsCheckedChanged += OnFilterChanged;
        if (ShowServerVaultCheck != null)
            ShowServerVaultCheck.IsCheckedChanged += OnFilterChanged;
    }

    private void OnFilterChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        OnFilterOptionsChanged();
    }

    protected override async Task<List<FileBrowserEntry>> LoadFilesFromModuleAsync(string modulePath)
    {
        // Reset vault entries when module changes
        _vaultEntries.Clear();

        return await Task.Run(() =>
        {
            var entries = new List<FileBrowserEntry>();

            try
            {
                if (!Directory.Exists(modulePath))
                    return entries;

                // Load UTC files (creature blueprints) from module
                var utcFiles = Directory.GetFiles(modulePath, "*.utc", SearchOption.TopDirectoryOnly);
                foreach (var file in utcFiles)
                {
                    entries.Add(new CreatureBrowserEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        Source = "Module",
                        IsFromHak = false,
                        IsBic = false
                    });
                }

                // Load BIC files from module directory (rare but possible)
                var bicFiles = Directory.GetFiles(modulePath, "*.bic", SearchOption.TopDirectoryOnly);
                foreach (var file in bicFiles)
                {
                    entries.Add(new CreatureBrowserEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        Source = "Module",
                        IsFromHak = false,
                        IsBic = true
                    });
                }

                entries = entries.OrderBy(e => e.Name).ToList();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"CreatureBrowserPanel: Found {entries.Count} creatures in module");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"CreatureBrowserPanel: Error loading creatures: {ex.Message}");
            }

            return entries;
        });
    }

    protected override async Task<List<FileBrowserEntry>> LoadAdditionalFilesAsync()
    {
        await LoadVaultEntriesAsync();
        return _vaultEntries.Cast<FileBrowserEntry>().ToList();
    }

    private async Task LoadVaultEntriesAsync()
    {
        await Task.Run(() =>
        {
            _vaultEntries.Clear();

            // Load from localvault
            var localVaultPath = GetLocalVaultPath();
            if (!string.IsNullOrEmpty(localVaultPath) && Directory.Exists(localVaultPath))
            {
                try
                {
                    var bicFiles = Directory.GetFiles(localVaultPath, "*.bic", SearchOption.TopDirectoryOnly);
                    foreach (var file in bicFiles)
                    {
                        _vaultEntries.Add(new CreatureBrowserEntry
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            FilePath = file,
                            Source = "LocalVault",
                            IsFromHak = false,
                            IsBic = true
                        });
                    }
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"CreatureBrowserPanel: Found {bicFiles.Length} BICs in localvault");
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Error scanning localvault: {ex.Message}");
                }
            }

            // Load from servervault (scan player subdirectories)
            var serverVaultPath = GetServerVaultPath();
            if (!string.IsNullOrEmpty(serverVaultPath) && Directory.Exists(serverVaultPath))
            {
                try
                {
                    foreach (var playerDir in Directory.GetDirectories(serverVaultPath))
                    {
                        var bicFiles = Directory.GetFiles(playerDir, "*.bic", SearchOption.TopDirectoryOnly);
                        var playerName = Path.GetFileName(playerDir);

                        foreach (var file in bicFiles)
                        {
                            _vaultEntries.Add(new CreatureBrowserEntry
                            {
                                Name = $"{Path.GetFileNameWithoutExtension(file)} ({playerName})",
                                FilePath = file,
                                Source = "ServerVault",
                                IsFromHak = false,
                                IsBic = true
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Error scanning servervault: {ex.Message}");
                }
            }
        });
    }

    private string? GetLocalVaultPath()
    {
        var nwnPath = _context?.NeverwinterNightsPath;
        if (string.IsNullOrEmpty(nwnPath))
            return null;

        var localVault = Path.Combine(nwnPath, "localvault");
        return Directory.Exists(localVault) ? localVault : null;
    }

    private string? GetServerVaultPath()
    {
        var nwnPath = _context?.NeverwinterNightsPath;
        if (string.IsNullOrEmpty(nwnPath))
            return null;

        var serverVault = Path.Combine(nwnPath, "servervault");
        return Directory.Exists(serverVault) ? serverVault : null;
    }

    protected override IEnumerable<FileBrowserEntry> ApplyCustomFilters(IEnumerable<FileBrowserEntry> entries)
    {
        bool showModule = ShowModuleCheck?.IsChecked == true;
        bool showLocalVault = ShowLocalVaultCheck?.IsChecked == true;
        bool showServerVault = ShowServerVaultCheck?.IsChecked == true;

        return entries.Where(e =>
        {
            if (e is CreatureBrowserEntry ce)
            {
                return (ce.Source == "Module" && showModule) ||
                       (ce.Source == "LocalVault" && showLocalVault) ||
                       (ce.Source == "ServerVault" && showServerVault);
            }
            return showModule; // Default to module filter for base entries
        });
    }

    protected override string FormatCountLabel(int moduleCount, int hakCount, int totalCount)
    {
        if (totalCount == 0)
        {
            if (string.IsNullOrEmpty(ModulePath))
                return "No module loaded";
            return "No creatures found";
        }

        // For creatures, count by source type
        var parts = new List<string>();
        if (moduleCount > 0) parts.Add($"{moduleCount} module");

        // Vault counts are in hakCount parameter (reusing for additional sources)
        var localCount = _vaultEntries.Count(e => e.Source == "LocalVault");
        var serverCount = _vaultEntries.Count(e => e.Source == "ServerVault");

        if (localCount > 0 && ShowLocalVaultCheck?.IsChecked == true)
            parts.Add($"{localCount} vault");
        if (serverCount > 0 && ShowServerVaultCheck?.IsChecked == true)
            parts.Add($"{serverCount} server");

        return string.Join(" + ", parts);
    }
}
