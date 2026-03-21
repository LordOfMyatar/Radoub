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
using Radoub.Formats.Resolver;
using Radoub.Formats.Services;
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
    /// True if this resource is from a base game BIF file.
    /// </summary>
    public bool IsFromBif { get; set; }

    /// <summary>
    /// Type indicator for display.
    /// </summary>
    public string TypeIndicator
    {
        get
        {
            if (IsBic) return $"[BIC:{Source}]";
            if (IsFromBif) return "[BIF]";
            if (IsFromHak) return "[HAK]";
            return "[UTC]";
        }
    }

    public override string DisplayName => (IsFromHak || IsFromBif) ? $"{Name} {TypeIndicator}" : (IsBic ? $"{Name} {TypeIndicator}" : Name);
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
    private readonly CheckBox _showDmVaultCheck;
    private readonly CheckBox _showHakCheck;
    private readonly CheckBox _showBifCheck;
    private List<CreatureBrowserEntry> _vaultEntries = new();
    private List<CreatureBrowserEntry> _hakEntries = new();
    private List<CreatureBrowserEntry> _bifEntries = new();
    private bool _showHakCreatures;
    private bool _hakCreaturesLoaded;
    private bool _showBifCreatures;
    private bool _bifCreaturesLoaded;

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
            Content = "Module",
            IsChecked = true
        };
        ToolTip.SetTip(_showModuleCheck, "Show .utc and .bic files from module folder");
        _showLocalVaultCheck = new CheckBox
        {
            Content = "Local Vault",
            IsChecked = false
        };
        ToolTip.SetTip(_showLocalVaultCheck, "Show .bic files from localvault");
        _showServerVaultCheck = new CheckBox
        {
            Content = "Server Vault",
            IsChecked = false
        };
        ToolTip.SetTip(_showServerVaultCheck, "Show .bic files from servervault subdirectories");
        _showDmVaultCheck = new CheckBox
        {
            Content = "DM Vault",
            IsChecked = false
        };
        ToolTip.SetTip(_showDmVaultCheck, "Show .bic files from dmvault");
        _showHakCheck = new CheckBox
        {
            Content = "HAK",
            IsChecked = false
        };
        ToolTip.SetTip(_showHakCheck, "Show .utc files from HAK archives");
        _showBifCheck = new CheckBox
        {
            Content = "Base Game",
            IsChecked = false
        };
        ToolTip.SetTip(_showBifCheck, "Show .utc creature blueprints from base game BIF archives");

        _showModuleCheck.IsCheckedChanged += OnFilterChanged;
        _showLocalVaultCheck.IsCheckedChanged += OnFilterChanged;
        _showServerVaultCheck.IsCheckedChanged += OnFilterChanged;
        _showDmVaultCheck.IsCheckedChanged += OnFilterChanged;
        _showHakCheck.IsCheckedChanged += OnShowHakChanged;
        _showBifCheck.IsCheckedChanged += OnShowBifChanged;

        // Create filter options panel
        var filterPanel = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 4, 0, 0)
        };
        filterPanel.Children.Add(_showModuleCheck);
        filterPanel.Children.Add(_showLocalVaultCheck);
        filterPanel.Children.Add(_showServerVaultCheck);
        filterPanel.Children.Add(_showDmVaultCheck);
        filterPanel.Children.Add(_showHakCheck);
        filterPanel.Children.Add(_showBifCheck);

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

    /// <summary>
    /// Gets or sets the game data service for BIF resource access.
    /// Must be set before BIF scanning will work.
    /// </summary>
    public IGameDataService? GameDataService { get; set; }

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
            MergeAdditionalEntries(_hakEntries);
        }

        OnFilterOptionsChanged();
    }

    private async void OnShowBifChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _showBifCreatures = _showBifCheck.IsChecked == true;
        UnifiedLogger.LogApplication(LogLevel.INFO, $"CreatureBrowserPanel: Show BIF creatures = {_showBifCreatures}");

        if (_showBifCreatures && !_bifCreaturesLoaded)
        {
            await LoadBifCreaturesAsync();
            MergeAdditionalEntries(_bifEntries);
        }

        OnFilterOptionsChanged();
    }

    protected override async Task<List<FileBrowserEntry>> LoadFilesFromModuleAsync(string modulePath)
    {
        // Reset vault, HAK, and BIF entries when module changes
        _vaultEntries.Clear();
        _hakEntries.Clear();
        _bifEntries.Clear();
        _hakCreaturesLoaded = false;
        _bifCreaturesLoaded = false;

        // Load vault entries here (not in LoadAdditionalFilesAsync) so they bypass
        // the base class name-based dedup. Vault BICs can share names with module UTCs
        // and both should appear in the list as separate entries.
        await LoadVaultEntriesAsync();

        return await Task.Run(() =>
        {
            var entries = new List<FileBrowserEntry>();

            try
            {
                if (!Directory.Exists(modulePath))
                {
                    // Still return vault entries even if module path is invalid
                    entries.AddRange(_vaultEntries);
                    return entries;
                }

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

                UnifiedLogger.LogApplication(LogLevel.INFO, $"CreatureBrowserPanel: Found {entries.Count} creatures in module");

                // Add vault entries - these are from different sources so no dedup
                entries.AddRange(_vaultEntries);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"CreatureBrowserPanel: Added {_vaultEntries.Count} vault entries");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"CreatureBrowserPanel: Error loading creatures: {ex.Message}");
            }

            return entries.OrderBy(e => e.Source).ThenBy(e => e.Name).ToList();
        });
    }

    protected override async Task<List<FileBrowserEntry>> LoadAdditionalFilesAsync()
    {
        // Vault entries are loaded in LoadFilesFromModuleAsync to bypass base class
        // name-based dedup. Only HAK/BIF entries go through additional loading (dedup
        // by name IS desired - HAK/BIF overrides should merge with module).
        if (_showHakCreatures && !_hakCreaturesLoaded)
        {
            await LoadHakCreaturesAsync();
        }

        if (_showBifCreatures && !_bifCreaturesLoaded)
        {
            await LoadBifCreaturesAsync();
        }

        var additional = new List<FileBrowserEntry>();
        additional.AddRange(_hakEntries);
        additional.AddRange(_bifEntries);
        return additional;
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
                    int serverVaultCount = 0;
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
                            serverVaultCount++;
                        }
                    }
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"CreatureBrowserPanel: Found {serverVaultCount} BICs in servervault ({Directory.GetDirectories(serverVaultPath).Length} player dirs)");
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Error scanning servervault: {ex.Message}");
                }
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"CreatureBrowserPanel: servervault not found (path: {serverVaultPath ?? "null"})");
            }

            // Load from dmvault (#1683)
            var dmVaultPath = GetDmVaultPath();
            if (!string.IsNullOrEmpty(dmVaultPath) && Directory.Exists(dmVaultPath))
            {
                try
                {
                    var bicFiles = Directory.GetFiles(dmVaultPath, "*.bic", SearchOption.TopDirectoryOnly);
                    foreach (var file in bicFiles)
                    {
                        _vaultEntries.Add(new CreatureBrowserEntry
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            FilePath = file,
                            Source = "DmVault",
                            IsFromHak = false,
                            IsBic = true
                        });
                    }
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"CreatureBrowserPanel: Found {bicFiles.Length} BICs in dmvault");
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Error scanning dmvault: {ex.Message}");
                }
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"CreatureBrowserPanel: dmvault not found (path: {dmVaultPath ?? "null"})");
            }
        });
    }

    private string? GetLocalVaultPath()
    {
        var nwnPath = _context?.NeverwinterNightsPath ?? RadoubSettings.Instance.NeverwinterNightsPath;
        if (string.IsNullOrEmpty(nwnPath))
            return null;

        var localVault = Path.Combine(nwnPath, "localvault");
        return Directory.Exists(localVault) ? localVault : null;
    }

    private string? GetServerVaultPath()
    {
        var nwnPath = _context?.NeverwinterNightsPath ?? RadoubSettings.Instance.NeverwinterNightsPath;
        if (string.IsNullOrEmpty(nwnPath))
            return null;

        var serverVault = Path.Combine(nwnPath, "servervault");
        return Directory.Exists(serverVault) ? serverVault : null;
    }

    private string? GetDmVaultPath()
    {
        var nwnPath = _context?.NeverwinterNightsPath ?? RadoubSettings.Instance.NeverwinterNightsPath;
        if (string.IsNullOrEmpty(nwnPath))
            return null;

        var dmVault = Path.Combine(nwnPath, "dmvault");
        return Directory.Exists(dmVault) ? dmVault : null;
    }

    private async Task LoadHakCreaturesAsync()
    {
        try
        {
            _hakEntries.Clear();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Only scan HAKs referenced by module.ifo (#1685)
            if (string.IsNullOrEmpty(ModulePath) || !Directory.Exists(ModulePath))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "CreatureBrowserPanel: No module path for HAK scanning");
                _hakCreaturesLoaded = true;
                return;
            }

            var hakSearchPaths = RadoubSettings.Instance.GetAllHakSearchPaths().ToList();
            var hakPaths = ModuleHakResolver.ResolveModuleHakPaths(ModulePath, hakSearchPaths);

            if (hakPaths.Count == 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "CreatureBrowserPanel: No module-referenced HAK files found");
                _hakCreaturesLoaded = true;
                return;
            }

            ShowLoading($"Scanning {hakPaths.Count} module HAK files...");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"[TIMING] CreatureBrowserPanel: Starting HAK scan of {hakPaths.Count} module-referenced files");

            for (int i = 0; i < hakPaths.Count; i++)
            {
                var hakPath = hakPaths[i];
                var hakName = Path.GetFileName(hakPath);
                UpdateLoadingStatus($"HAK {i + 1}/{hakPaths.Count}: {hakName}...");

                await Task.Run(() => ScanHakForCreatures(hakPath));
            }

            _hakEntries = _hakEntries.OrderBy(s => s.Name).ToList();
            _hakCreaturesLoaded = true;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"[TIMING] CreatureBrowserPanel: HAK scan complete — {_hakEntries.Count} creatures from {hakPaths.Count} HAKs in {sw.ElapsedMilliseconds}ms (metadata-only)");
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

    private async Task LoadBifCreaturesAsync()
    {
        if (GameDataService == null || !GameDataService.IsConfigured)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "CreatureBrowserPanel: GameDataService not available for BIF scanning");
            _bifCreaturesLoaded = true;
            return;
        }

        try
        {
            _bifEntries.Clear();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ShowLoading("Scanning base game creatures...");

            await Task.Run(() =>
            {
                var resources = GameDataService.ListResources(ResourceTypes.Utc)
                    .Where(r => r.Source == GameResourceSource.Bif)
                    .ToList();

                foreach (var resource in resources)
                {
                    _bifEntries.Add(new CreatureBrowserEntry
                    {
                        Name = resource.ResRef,
                        Source = "Base Game",
                        IsFromHak = false,
                        IsFromBif = true,
                        IsBic = false
                    });
                }

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"[TIMING] CreatureBrowserPanel: BIF scan — {_bifEntries.Count} creatures in {sw.ElapsedMilliseconds}ms (KEY cache lookup)");
            });

            _bifEntries = _bifEntries.OrderBy(e => e.Name).ToList();
            _bifCreaturesLoaded = true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"CreatureBrowserPanel: Failed to load BIF creatures: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    protected override IEnumerable<FileBrowserEntry> ApplyCustomFilters(IEnumerable<FileBrowserEntry> entries)
    {
        bool showModule = _showModuleCheck.IsChecked == true;
        bool showLocalVault = _showLocalVaultCheck.IsChecked == true;
        bool showServerVault = _showServerVaultCheck.IsChecked == true;
        bool showDmVault = _showDmVaultCheck.IsChecked == true;
        bool showHak = _showHakCheck.IsChecked == true;
        bool showBif = _showBifCheck.IsChecked == true;

        return entries.Where(e =>
        {
            if (e is CreatureBrowserEntry ce)
            {
                if (ce.IsFromBif)
                    return showBif;
                if (ce.IsFromHak)
                    return showHak;
                return (ce.Source == "Module" && showModule) ||
                       (ce.Source == "LocalVault" && showLocalVault) ||
                       (ce.Source == "ServerVault" && showServerVault) ||
                       (ce.Source == "DmVault" && showDmVault);
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

        // Base class moduleCount includes vault entries (anything !IsFromHak).
        // Compute accurate counts from Source field instead.
        var parts = new List<string>();

        var bifCount = _showBifCreatures ? _bifEntries.Count : 0;

        // Module count = total minus vault minus HAK minus BIF
        var actualModuleCount = totalCount - hakCount - bifCount
            - _vaultEntries.Count(e => e.Source == "LocalVault" && _showLocalVaultCheck.IsChecked == true)
            - _vaultEntries.Count(e => e.Source == "ServerVault" && _showServerVaultCheck.IsChecked == true)
            - _vaultEntries.Count(e => e.Source == "DmVault" && _showDmVaultCheck.IsChecked == true);
        if (actualModuleCount > 0 && _showModuleCheck.IsChecked == true)
            parts.Add($"{actualModuleCount} module");

        var localCount = _vaultEntries.Count(e => e.Source == "LocalVault");
        if (localCount > 0 && _showLocalVaultCheck.IsChecked == true)
            parts.Add($"{localCount} vault");

        var serverCount = _vaultEntries.Count(e => e.Source == "ServerVault");
        if (serverCount > 0 && _showServerVaultCheck.IsChecked == true)
            parts.Add($"{serverCount} server");

        var dmCount = _vaultEntries.Count(e => e.Source == "DmVault");
        if (dmCount > 0 && _showDmVaultCheck.IsChecked == true)
            parts.Add($"{dmCount} DM");

        if (hakCount > 0 && _showHakCheck.IsChecked == true)
            parts.Add($"{hakCount} HAK");

        if (bifCount > 0 && _showBifCheck.IsChecked == true)
            parts.Add($"{bifCount} base game");

        return string.Join(" + ", parts);
    }
}
