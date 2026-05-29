using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Gff;
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
public class CreatureBrowserPanel : FileBrowserPanelBase, IBrowserRowRefresher
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

    // Static cache for HAK file contents - persists across panel instances.
    // ConcurrentDictionary so concurrent panel instances can safely race on
    // Task.Run scans (#2262).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CreatureHakCacheEntry> _hakCache = new();

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

    protected override bool SupportsCopyToModule() => true;

    protected override bool IsArchiveEntry(FileBrowserEntry entry)
        => entry is CreatureBrowserEntry c && (c.IsFromHak || c.IsFromBif);

    protected override Task<byte[]?> ExtractArchiveBytesAsync(FileBrowserEntry entry)
    {
        if (entry is not CreatureBrowserEntry ce) return Task.FromResult<byte[]?>(null);

        if (ce.IsFromBif && GameDataService is { IsConfigured: true })
        {
            return Task.FromResult(GameDataService.FindResource(ce.Name, ResourceTypes.Utc));
        }
        if (ce.IsFromHak && !string.IsNullOrEmpty(ce.HakPath))
        {
            return Task.FromResult(ExtractFromHak(ce.HakPath, ce.Name, ResourceTypes.Utc));
        }
        return Task.FromResult<byte[]?>(null);
    }

    protected override Task<(string tag, string name)> ReadSourceMetadataAsync(byte[] bytes)
    {
        try
        {
            var (tag, name) = ReadCreatureMetadataFromGff(bytes);
            return Task.FromResult((tag, name));
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"CreatureBrowserPanel.ReadSourceMetadataAsync: {ex.Message}");
            return Task.FromResult((string.Empty, string.Empty));
        }
    }

    /// <summary>
    /// Pull Tag + FirstName/LastName from a UTC- or BIC-formatted GFF byte
    /// buffer. Bypasses UtcReader/BicReader file-type validation so a single
    /// path handles both resource types — they share Tag, FirstName, and
    /// LastName field names at the root struct.
    /// </summary>
    private static (string tag, string name) ReadCreatureMetadataFromGff(byte[] bytes)
    {
        var gff = GffReader.Read(bytes);
        var root = gff.RootStruct;
        var tag = root.GetFieldValue<string>("Tag", string.Empty) ?? string.Empty;

        var firstNameField = root.GetField("FirstName");
        var firstName = firstNameField?.Value is CExoLocString fn
            ? fn.GetDefault() ?? string.Empty
            : string.Empty;

        var lastNameField = root.GetField("LastName");
        var lastName = lastNameField?.Value is CExoLocString ln
            ? ln.GetDefault() ?? string.Empty
            : string.Empty;

        var fullName = string.IsNullOrEmpty(lastName) ? firstName : $"{firstName} {lastName}".Trim();
        return (tag, fullName);
    }

    /// <summary>
    /// Optional shared UTC palette cache. When provided, BIF/HAK entries pull
    /// Tag/DisplayLabel from the cache (zero disk I/O) before falling back to
    /// GFF extraction. Module + vault entries always read GFF directly.
    /// Callers should construct a <see cref="SharedPaletteCacheService"/>
    /// pointing at a UTC-specific subdirectory (e.g. ~/Radoub/Cache/CreaturePalette/)
    /// so it does not collide with the UTI/UTM caches.
    /// </summary>
    public ISharedPaletteCacheService? PaletteCache { get; set; }

    /// <summary>
    /// Background pass: populate Tag + DisplayLabel on every entry that does
    /// not yet have metadata. Yields every 50 entries to keep the UI thread
    /// responsive. Honors <paramref name="cancellationToken"/> between batches.
    /// </summary>
    protected override async Task IndexMetadataAsync(
        IReadOnlyList<FileBrowserEntry> entries,
        CancellationToken cancellationToken)
    {
        var paletteLookup = BuildPaletteLookup();
        int processed = 0;

        foreach (var entry in entries)
        {
            if (cancellationToken.IsCancellationRequested) return;
            if (entry.MetadataLoaded) { processed++; continue; }

            try
            {
                if (TryFillFromCache(entry, paletteLookup))
                {
                    entry.MetadataLoaded = true;
                }
                else
                {
                    var bytes = await ReadEntryBytesAsync(entry, cancellationToken);
                    if (bytes != null)
                    {
                        var (tag, name) = await ReadSourceMetadataAsync(bytes);
                        entry.Tag = tag;
                        entry.DisplayLabel = name;
                    }
                    entry.MetadataLoaded = true;
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"CreatureBrowserPanel.IndexMetadataAsync({entry.Name}): {ex.Message}");
                entry.MetadataLoaded = true; // don't retry forever
            }

            processed++;
            if (processed % 50 == 0)
            {
                await Task.Yield();
            }
        }
    }

    /// <summary>
    /// Build a ResRef → cache-item lookup from the active palette cache. Returns
    /// an empty dictionary when no cache is wired or the cache is empty.
    /// </summary>
    private Dictionary<string, SharedPaletteCacheItem> BuildPaletteLookup()
    {
        if (PaletteCache == null) return new Dictionary<string, SharedPaletteCacheItem>();

        var items = PaletteCache.GetAggregatedCache();
        if (items == null || items.Count == 0)
            return new Dictionary<string, SharedPaletteCacheItem>();

        var dict = new Dictionary<string, SharedPaletteCacheItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            dict.TryAdd(item.ResRef, item);
        }
        return dict;
    }

    /// <summary>
    /// Pure-logic test seam: try to populate Tag + DisplayLabel from a cache
    /// lookup. Returns true on hit, false on miss. Lookup is keyed by ResRef
    /// (case-insensitive — caller responsible for using OrdinalIgnoreCase).
    /// </summary>
    internal static bool TryFillFromCache(
        FileBrowserEntry entry,
        Dictionary<string, SharedPaletteCacheItem> lookup)
    {
        if (lookup.Count == 0) return false;
        if (!lookup.TryGetValue(entry.Name, out var item)) return false;

        entry.Tag = item.Tag ?? string.Empty;
        entry.DisplayLabel = item.DisplayName ?? string.Empty;
        return true;
    }

    private async Task<byte[]?> ReadEntryBytesAsync(
        FileBrowserEntry entry,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(entry.FilePath) && File.Exists(entry.FilePath))
        {
            return await File.ReadAllBytesAsync(entry.FilePath, cancellationToken);
        }

        // HAK/BIF/archive entry — synchronous extraction wrapped in Task.Run
        return await Task.Run(() => ExtractCreatureArchiveBytes(entry, GameDataService), cancellationToken);
    }

    /// <summary>
    /// Route an archive-sourced CreatureBrowserEntry to the correct extraction path
    /// (BIF via GameDataService, HAK via shared ExtractFromHak helper). Returns
    /// null when the entry is not archive-sourced or required dependencies are missing.
    /// </summary>
    private static byte[]? ExtractCreatureArchiveBytes(FileBrowserEntry entry, IGameDataService? gameDataService)
    {
        if (entry is not CreatureBrowserEntry creatureEntry) return null;

        if (creatureEntry.IsFromBif)
        {
            if (gameDataService is { IsConfigured: true })
                return gameDataService.FindResource(creatureEntry.Name, ResourceTypes.Utc);
            return null;
        }

        if (creatureEntry.IsFromHak && !string.IsNullOrEmpty(creatureEntry.HakPath))
            return ExtractFromHak(creatureEntry.HakPath, creatureEntry.Name, ResourceTypes.Utc);

        return null;
    }

    /// <summary>
    /// Re-read metadata for a single entry — called by the host tool after a
    /// save so the browser row reflects the new Tag/Name without a full reindex.
    /// </summary>
    public override async Task RefreshEntryMetadataAsync(FileBrowserEntry entry)
    {
        try
        {
            var bytes = await ReadEntryBytesAsync(entry, CancellationToken.None);
            if (bytes == null) return;
            var (tag, name) = await ReadSourceMetadataAsync(bytes);
            entry.Tag = tag;
            entry.DisplayLabel = name;
            entry.MetadataLoaded = true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"CreatureBrowserPanel.RefreshEntryMetadataAsync({entry.Name}): {ex.Message}");
        }
    }

    /// <summary>
    /// <see cref="IBrowserRowRefresher"/> implementation. Host tools should
    /// depend on the interface (via <see cref="BrowserSaveNotifier"/>) rather
    /// than calling the static method directly, so the post-save wire-up is
    /// testable with a fake refresher.
    /// </summary>
    public Task RefreshRowAsync(string filePath)
    {
        var entry = FindEntryByFilePath(filePath);
        return entry == null ? Task.CompletedTask : RefreshEntryFromDiskAsync(entry);
    }

    /// <summary>
    /// Pure-logic helper: parse a UTC or BIC byte blob into a
    /// <see cref="SharedPaletteCacheItem"/> for cache persistence. Returns null
    /// on parse failure so the populator can skip corrupt entries without
    /// aborting an entire HAK scan. DisplayName uses the canonical
    /// "{FirstName} {LastName}".Trim() formatting (matches
    /// CreatureDisplayService.GetCreatureFullName). BIC and UTC share Tag,
    /// FirstName, and LastName fields, so a single helper handles both.
    /// </summary>
    public static SharedPaletteCacheItem? BuildPaletteItemFromUtc(
        byte[] bytes,
        string resRef,
        string sourceLocation)
    {
        try
        {
            var (tag, fullName) = ReadCreatureMetadataFromGff(bytes);
            return new SharedPaletteCacheItem
            {
                ResRef = resRef,
                Tag = tag,
                DisplayName = fullName,
                SourceLocation = sourceLocation
            };
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"CreatureBrowserPanel.BuildPaletteItemFromUtc({resRef}): {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Save-flow hook for module + vault entries: re-read Tag + DisplayLabel
    /// from the on-disk UTC/BIC bytes. Pure-static so host tools (Quartermaster)
    /// can call without holding a CreatureBrowserPanel reference, and so the
    /// round-trip is unit-testable without Avalonia (#2201). Handles both UTC
    /// and BIC formats — they share Tag/FirstName/LastName field layout.
    ///
    /// No-op for entries without a FilePath (HAK/BIF rows are cache-driven).
    /// On read or parse failure the entry is left untouched.
    /// </summary>
    public static async Task RefreshEntryFromDiskAsync(FileBrowserEntry entry)
    {
        if (string.IsNullOrEmpty(entry.FilePath)) return;
        if (!File.Exists(entry.FilePath)) return;

        try
        {
            var bytes = await File.ReadAllBytesAsync(entry.FilePath);
            var (tag, fullName) = ReadCreatureMetadataFromGff(bytes);
            entry.Tag = tag;
            entry.DisplayLabel = fullName;
            entry.MetadataLoaded = true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"CreatureBrowserPanel.RefreshEntryFromDiskAsync({entry.Name}): {ex.Message}");
        }
    }

    /// <summary>
    /// True when the persistent palette cache for this HAK is missing or stale —
    /// the populator should extract+parse the UTCs and write the cache. False
    /// when no PaletteCache is wired or the existing cache is fresh.
    /// </summary>
    private bool ShouldPopulatePaletteCacheForHak(string hakPath, DateTime lastModified)
    {
        if (PaletteCache == null) return false;
        if (PaletteCache.HasValidSourceCache("hak", hakPath)) return false;
        return true;
    }

    protected override Task<byte[]> ApplyCopyCustomizationsAsync(byte[] sourceBytes, CopyToModuleResult result)
        => Task.FromResult(ApplyUtcCopyCustomizations(sourceBytes, result));

    /// <summary>
    /// Rewrite a UTC byte blob with the user's new TemplateResRef/Tag/FirstName.
    /// The dialog's "Name" maps to FirstName; LastName is left unchanged because
    /// the dialog does not expose a separate last-name field.
    /// </summary>
    internal static byte[] ApplyUtcCopyCustomizations(byte[] sourceBytes, CopyToModuleResult result)
    {
        var utc = Radoub.Formats.Utc.UtcReader.Read(sourceBytes);
        utc.TemplateResRef = result.NewResRef;
        if (result.NewTag != null) utc.Tag = result.NewTag;
        if (result.NewName != null) utc.FirstName.SetString(0, result.NewName);
        return Radoub.Formats.Utc.UtcWriter.Write(utc);
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

            // Persistent palette cache: populate Tag/DisplayName for instant
            // sort/search on subsequent panel loads (#2201).
            var populate = ShouldPopulatePaletteCacheForHak(hakPath, lastModified);
            var paletteItems = populate ? new List<SharedPaletteCacheItem>() : null;

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

                if (populate && paletteItems != null)
                {
                    var bytes = ExtractFromHak(hakPath, resource.ResRef, ResourceTypes.Utc);
                    if (bytes != null)
                    {
                        var item = BuildPaletteItemFromUtc(bytes, resource.ResRef, $"HAK: {hakFileName}");
                        if (item != null) paletteItems.Add(item);
                    }
                }

                // Skip if already have this creature
                if (_hakEntries.Any(c => c.Name.Equals(resource.ResRef, StringComparison.OrdinalIgnoreCase)))
                    continue;

                _hakEntries.Add(creatureEntry);
            }

            _hakCache[hakPath] = newCacheEntry;

            if (populate && paletteItems != null && PaletteCache != null)
            {
                _ = PaletteCache.SaveSourceCacheAsync(
                    "hak",
                    paletteItems,
                    validationPath: hakPath,
                    sourceModified: lastModified);
            }

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

            // Capture GameDataService into local for closure use inside Task.Run
            var gameDataService = GameDataService;
            var populate = PaletteCache != null && !PaletteCache.HasValidSourceCache("bif");
            List<SharedPaletteCacheItem>? paletteItems = null;

            await Task.Run(() =>
            {
                var resources = gameDataService.ListResources(ResourceTypes.Utc)
                    .Where(r => r.Source == GameResourceSource.Bif)
                    .ToList();

                if (populate) paletteItems = new List<SharedPaletteCacheItem>();

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

                    if (populate && paletteItems != null)
                    {
                        var bytes = gameDataService.FindResource(resource.ResRef, ResourceTypes.Utc);
                        if (bytes != null)
                        {
                            var item = BuildPaletteItemFromUtc(bytes, resource.ResRef, "Base Game");
                            if (item != null) paletteItems.Add(item);
                        }
                    }
                }

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"[TIMING] CreatureBrowserPanel: BIF scan — {_bifEntries.Count} creatures in {sw.ElapsedMilliseconds}ms (KEY cache lookup)");
            });

            if (populate && paletteItems != null && PaletteCache != null)
            {
                _ = PaletteCache.SaveSourceCacheAsync("bif", paletteItems);
            }

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
