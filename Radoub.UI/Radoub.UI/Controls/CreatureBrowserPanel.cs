using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
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
    public string TypeIndicator
    {
        get
        {
            if (IsBic) return $"[BIC:{Source}]";
            if (IsFromHak) return "[HAK]";
            return "[UTC]";
        }
    }

    public override string DisplayName => IsFromHak ? $"{Name} {TypeIndicator}" : (IsBic ? $"{Name} {TypeIndicator}" : Name);
}

/// <summary>
/// Cached HAK file creature data to avoid re-scanning on each refresh.
/// </summary>
internal class CreatureHakCacheEntry
{
    public string HakPath { get; set; } = "";
    public DateTime LastModified { get; set; }
    public List<CreatureBrowserEntry> Creatures { get; set; } = new();
}

/// <summary>
/// Creature browser panel for embedding in Quartermaster's main window.
/// Provides .utc/.bic file list from module, vaults, and HAKs.
/// </summary>
public class CreatureBrowserPanel : FileBrowserPanelBase
{
    private readonly IScriptBrowserContext? _context;
    private readonly CheckBox _showModuleCheck;
    private readonly CheckBox _showLocalVaultCheck;
    private readonly CheckBox _showServerVaultCheck;
    private readonly CheckBox _showHakCheck;
    private List<CreatureBrowserEntry> _vaultEntries = new();
    private List<CreatureBrowserEntry> _hakEntries = new();
    private bool _showHakCreatures;
    private bool _hakCreaturesLoaded;

    // Static cache for HAK file contents - persists across panel instances
    private static readonly Dictionary<string, CreatureHakCacheEntry> _hakCache = new();

    public CreatureBrowserPanel() : this(null)
    {
    }

    public CreatureBrowserPanel(IScriptBrowserContext? context)
    {
        _context = context;

        FileExtension = ".utc";
        SearchWatermark = "Type to filter creatures...";
        HeaderTextContent = "Creatures";

        // Create filter checkboxes
        _showModuleCheck = new CheckBox
        {
            Content = "Module (.utc)",
            IsChecked = true
        };
        _showLocalVaultCheck = new CheckBox
        {
            Content = "Local Vault (.bic)",
            IsChecked = true
        };
        _showServerVaultCheck = new CheckBox
        {
            Content = "Server Vault (.bic)",
            IsChecked = false
        };
        _showHakCheck = new CheckBox
        {
            Content = "HAK (.utc)",
            IsChecked = false
        };
        ToolTip.SetTip(_showHakCheck, "Include creature blueprints from HAK files");

        _showModuleCheck.IsCheckedChanged += OnFilterChanged;
        _showLocalVaultCheck.IsCheckedChanged += OnFilterChanged;
        _showServerVaultCheck.IsCheckedChanged += OnFilterChanged;
        _showHakCheck.IsCheckedChanged += OnShowHakChanged;

        // Create filter options panel
        var filterPanel = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 4, 0, 0)
        };
        filterPanel.Children.Add(_showModuleCheck);
        filterPanel.Children.Add(_showLocalVaultCheck);
        filterPanel.Children.Add(_showServerVaultCheck);
        filterPanel.Children.Add(_showHakCheck);

        FilterOptionsContent = filterPanel;
    }

    /// <summary>
    /// Gets or sets whether HAK creatures are shown.
    /// </summary>
    public bool ShowHakCreatures
    {
        get => _showHakCreatures;
        set
        {
            if (_showHakCreatures != value)
            {
                _showHakCreatures = value;
                _showHakCheck.IsChecked = value;
            }
        }
    }

    private void OnFilterChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        OnFilterOptionsChanged();
    }

    private async void OnShowHakChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _showHakCreatures = _showHakCheck.IsChecked == true;
        UnifiedLogger.LogApplication(LogLevel.INFO, $"CreatureBrowserPanel: Show HAK creatures = {_showHakCreatures}");

        if (_showHakCreatures && !_hakCreaturesLoaded)
        {
            await LoadHakCreaturesAsync();
        }

        OnFilterOptionsChanged();
    }

    protected override async Task<List<FileBrowserEntry>> LoadFilesFromModuleAsync(string modulePath)
    {
        // Reset vault and HAK entries when module changes
        _vaultEntries.Clear();
        _hakEntries.Clear();
        _hakCreaturesLoaded = false;

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

        if (_showHakCreatures && !_hakCreaturesLoaded)
        {
            await LoadHakCreaturesAsync();
        }

        // Combine vault and HAK entries
        var allEntries = new List<FileBrowserEntry>();
        allEntries.AddRange(_vaultEntries.Cast<FileBrowserEntry>());
        allEntries.AddRange(_hakEntries.Cast<FileBrowserEntry>());
        return allEntries;
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

    private async Task LoadHakCreaturesAsync()
    {
        try
        {
            _hakEntries.Clear();

            var hakPaths = new List<string>();

            // Current module directory
            if (!string.IsNullOrEmpty(ModulePath) && Directory.Exists(ModulePath))
            {
                hakPaths.AddRange(GetHakFilesFromPath(ModulePath));
            }

            // NWN user hak folder - use context if available, otherwise fall back to RadoubSettings
            var userPath = _context?.NeverwinterNightsPath ?? RadoubSettings.Instance.NeverwinterNightsPath;
            if (!string.IsNullOrEmpty(userPath) && Directory.Exists(userPath))
            {
                var hakFolder = Path.Combine(userPath, "hak");
                if (Directory.Exists(hakFolder))
                {
                    hakPaths.AddRange(GetHakFilesFromPath(hakFolder));
                }
            }

            hakPaths = hakPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (hakPaths.Count == 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "CreatureBrowserPanel: No HAK files found to scan");
                _hakCreaturesLoaded = true;
                return;
            }

            ShowLoading($"Scanning {hakPaths.Count} HAK files...");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"CreatureBrowserPanel: Scanning {hakPaths.Count} HAK files");

            for (int i = 0; i < hakPaths.Count; i++)
            {
                var hakPath = hakPaths[i];
                var hakName = Path.GetFileName(hakPath);
                UpdateLoadingStatus($"HAK {i + 1}/{hakPaths.Count}: {hakName}...");

                await Task.Run(() => ScanHakForCreatures(hakPath));
            }

            _hakEntries = _hakEntries.OrderBy(s => s.Name).ToList();
            _hakCreaturesLoaded = true;

            UnifiedLogger.LogApplication(LogLevel.INFO, $"CreatureBrowserPanel: Loaded {_hakEntries.Count} creatures from HAK files");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"CreatureBrowserPanel: Failed to load HAK creatures: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private IEnumerable<string> GetHakFilesFromPath(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                return Directory.GetFiles(path, "*.hak", SearchOption.TopDirectoryOnly);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Error scanning for HAKs in {UnifiedLogger.SanitizePath(path)}: {ex.Message}");
        }
        return Enumerable.Empty<string>();
    }

    private void ScanHakForCreatures(string hakPath)
    {
        try
        {
            var hakFileName = Path.GetFileName(hakPath);
            var lastModified = File.GetLastWriteTimeUtc(hakPath);

            // Check cache first
            if (_hakCache.TryGetValue(hakPath, out var cached) && cached.LastModified == lastModified)
            {
                foreach (var creature in cached.Creatures)
                {
                    // Skip if already have this creature from module or another HAK
                    if (_hakEntries.Any(c => c.Name.Equals(creature.Name, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    _hakEntries.Add(new CreatureBrowserEntry
                    {
                        Name = creature.Name,
                        Source = creature.Source,
                        IsFromHak = true,
                        HakPath = creature.HakPath,
                        IsBic = false
                    });
                }
                return;
            }

            // Scan HAK
            var erf = ErfReader.ReadMetadataOnly(hakPath);
            var utcResources = erf.GetResourcesByType(ResourceTypes.Utc).ToList();
            var newCacheEntry = new CreatureHakCacheEntry
            {
                HakPath = hakPath,
                LastModified = lastModified,
                Creatures = new List<CreatureBrowserEntry>()
            };

            foreach (var resource in utcResources)
            {
                var creatureEntry = new CreatureBrowserEntry
                {
                    Name = resource.ResRef,
                    Source = $"HAK: {hakFileName}",
                    IsFromHak = true,
                    HakPath = hakPath,
                    IsBic = false
                };

                newCacheEntry.Creatures.Add(creatureEntry);

                // Skip if already have this creature
                if (_hakEntries.Any(c => c.Name.Equals(resource.ResRef, StringComparison.OrdinalIgnoreCase)))
                    continue;

                _hakEntries.Add(creatureEntry);
            }

            _hakCache[hakPath] = newCacheEntry;

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"CreatureBrowserPanel: Cached {utcResources.Count} creatures from {hakFileName}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning HAK {UnifiedLogger.SanitizePath(hakPath)}: {ex.Message}");
        }
    }

    protected override IEnumerable<FileBrowserEntry> ApplyCustomFilters(IEnumerable<FileBrowserEntry> entries)
    {
        bool showModule = _showModuleCheck.IsChecked == true;
        bool showLocalVault = _showLocalVaultCheck.IsChecked == true;
        bool showServerVault = _showServerVaultCheck.IsChecked == true;
        bool showHak = _showHakCheck.IsChecked == true;

        return entries.Where(e =>
        {
            if (e is CreatureBrowserEntry ce)
            {
                if (ce.IsFromHak)
                    return showHak;
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

        // Vault counts
        var localCount = _vaultEntries.Count(e => e.Source == "LocalVault");
        var serverCount = _vaultEntries.Count(e => e.Source == "ServerVault");

        if (localCount > 0 && _showLocalVaultCheck.IsChecked == true)
            parts.Add($"{localCount} vault");
        if (serverCount > 0 && _showServerVaultCheck.IsChecked == true)
            parts.Add($"{serverCount} server");

        // HAK count
        if (hakCount > 0 && _showHakCheck.IsChecked == true)
            parts.Add($"{hakCount} HAK");

        return string.Join(" + ", parts);
    }
}
