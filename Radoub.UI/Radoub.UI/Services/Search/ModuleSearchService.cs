using System.Diagnostics;
using System.Text.RegularExpressions;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using Radoub.Formats.Search;

namespace Radoub.UI.Services.Search;

/// <summary>
/// Orchestrates search across files in a module directory.
/// Discovers GFF files, parses them, runs the appropriate search provider,
/// and aggregates results with progress reporting and cancellation support.
/// </summary>
public class ModuleSearchService
{
    private readonly SearchProviderFactory _factory;

    /// <summary>Searchable file extensions. GFF formats are parsed and searched by field;
    /// .nss is plain NWScript source, searched line-by-line as text (#2314).</summary>
    private static readonly HashSet<string> SearchableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dlg", ".utc", ".bic", ".uti", ".utm", ".jrl", ".ifo", ".fac",
        ".are", ".git", ".utp", ".ute", ".utt", ".utw", ".utd", ".uts", ".itp",
        ".nss"
    };

    /// <summary>Virtual field for NSS source-content matches (not a GFF field).</summary>
    private static readonly FieldDefinition NssSourceField = new()
    {
        Name = "Script Source",
        GffPath = "<nss-source>",
        FieldType = SearchFieldType.Text,
        Category = SearchFieldCategory.Script,
        Description = "NWScript source line",
        IsReplaceable = false
    };

    /// <summary>Map resource type to Radoub tool identifier for dispatch</summary>
    private static readonly Dictionary<ushort, string> ToolIdMap = new()
    {
        [ResourceTypes.Dlg] = "parley",
        [ResourceTypes.Utc] = "quartermaster",
        [ResourceTypes.Bic] = "quartermaster",
        [ResourceTypes.Uti] = "relique",
        [ResourceTypes.Utm] = "fence",
        [ResourceTypes.Jrl] = "manifest",
    };

    public ModuleSearchService()
        : this(SearchProviderFactory.CreateDefault())
    {
    }

    public ModuleSearchService(Func<string, string?>? utmItemNameResolver)
        : this(SearchProviderFactory.CreateDefault(utmItemNameResolver))
    {
    }

    public ModuleSearchService(SearchProviderFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Search all matching files in a module directory.
    /// </summary>
    /// <param name="modulePath">Path to the unpacked module directory</param>
    /// <param name="criteria">Search criteria including pattern and filters</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated search results</returns>
    public async Task<ModuleSearchResults> ScanModuleAsync(
        string modulePath,
        SearchCriteria criteria,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(modulePath))
            throw new ArgumentNullException(nameof(modulePath));
        if (!Directory.Exists(modulePath))
            throw new DirectoryNotFoundException($"Module directory not found: {modulePath}");

        var validationError = criteria.Validate();
        if (validationError != null)
            throw new ArgumentException($"Invalid search criteria: {validationError}", nameof(criteria));

        var sw = Stopwatch.StartNew();

        // Phase 1: Discover files
        progress?.Report(new ScanProgress { Phase = "Discovering files" });
        var files = DiscoverFiles(modulePath, criteria.FileTypeFilter);
        cancellationToken.ThrowIfCancellationRequested();

        // Phase 2: Search each file
        var results = new List<FileSearchResult>();
        int scanned = 0;
        int matchesSoFar = 0;

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(filePath);
            progress?.Report(new ScanProgress
            {
                Phase = "Searching",
                CurrentFile = fileName,
                FilesScanned = scanned,
                TotalFiles = files.Count,
                MatchesFound = matchesSoFar
            });

            var result = await Task.Run(() => SearchSingleFileInternal(filePath, criteria), cancellationToken);
            if (result != null && (result.MatchCount > 0 || result.HadParseError))
            {
                results.Add(result);
                matchesSoFar += result.MatchCount;
            }

            scanned++;
        }

        // Filename rename mode: add filename matches as an additional pass.
        // This is module-scoped (one call per module), not per-file.
        if (criteria.IncludeFilenameResRef)
        {
            var filenameProvider = new FilenameSearchProvider();
            var filenameResults = filenameProvider.Search(modulePath, criteria);

            foreach (var fnResult in filenameResults)
            {
                // If a per-file provider already returned a result for this file, merge filename
                // match into the existing result's Matches list. Otherwise, add as a new result.
                var existing = results.FirstOrDefault(r => r.FilePath == fnResult.FilePath);
                if (existing != null)
                {
                    var merged = new FileSearchResult
                    {
                        FilePath = existing.FilePath,
                        ResourceType = existing.ResourceType,
                        ToolId = existing.ToolId,
                        Matches = existing.Matches.Concat(fnResult.Matches).ToList(),
                        HadParseError = existing.HadParseError,
                        ParseError = existing.ParseError
                    };
                    var idx = results.IndexOf(existing);
                    results[idx] = merged;
                    matchesSoFar += fnResult.MatchCount;
                }
                else
                {
                    results.Add(fnResult);
                    matchesSoFar += fnResult.MatchCount;
                }
            }
        }

        sw.Stop();

        progress?.Report(new ScanProgress
        {
            Phase = "Complete",
            FilesScanned = scanned,
            TotalFiles = files.Count,
            MatchesFound = matchesSoFar
        });

        return new ModuleSearchResults
        {
            Files = results,
            TotalFilesScanned = scanned,
            WasCancelled = false,
            Duration = sw.Elapsed
        };
    }

    /// <summary>
    /// Search a single file and return results.
    /// </summary>
    public FileSearchResult SearchSingleFile(string filePath, SearchCriteria criteria)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        return SearchSingleFileInternal(filePath, criteria)
            ?? new FileSearchResult
            {
                FilePath = filePath,
                ResourceType = GetResourceType(filePath),
                ToolId = GetToolId(GetResourceType(filePath)),
                Matches = Array.Empty<SearchMatch>()
            };
    }

    private FileSearchResult? SearchSingleFileInternal(string filePath, SearchCriteria criteria)
    {
        var resourceType = GetResourceType(filePath);
        var toolId = GetToolId(resourceType);

        // NSS is plain NWScript source, not GFF — search it as text.
        if (resourceType == ResourceTypes.Nss)
            return SearchNssFileInternal(filePath, resourceType, toolId, criteria);

        try
        {
            var gffFile = GffReader.Read(filePath);
            var provider = _factory.GetProvider(resourceType);

            if (provider == null)
            {
                return new FileSearchResult
                {
                    FilePath = filePath,
                    ResourceType = resourceType,
                    ToolId = toolId,
                    Matches = Array.Empty<SearchMatch>(),
                    HadParseError = true,
                    ParseError = $"No search provider for resource type {resourceType}"
                };
            }

            var matches = provider.Search(gffFile, criteria);
            return new FileSearchResult
            {
                FilePath = filePath,
                ResourceType = resourceType,
                ToolId = toolId,
                Matches = matches
            };
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Failed to search {Path.GetFileName(filePath)}: {ex.Message}");

            return new FileSearchResult
            {
                FilePath = filePath,
                ResourceType = resourceType,
                ToolId = toolId,
                Matches = Array.Empty<SearchMatch>(),
                HadParseError = true,
                ParseError = ex.Message
            };
        }
    }

    /// <summary>
    /// Search NWScript (.nss) source as plain text, one match per regex hit, with line numbers.
    /// </summary>
    private FileSearchResult? SearchNssFileInternal(
        string filePath, ushort resourceType, string toolId, SearchCriteria criteria)
    {
        try
        {
            var source = File.ReadAllText(filePath);
            var regex = criteria.ToRegex();
            var matches = new List<SearchMatch>();

            foreach (Match m in regex.Matches(source))
            {
                matches.Add(new SearchMatch
                {
                    Field = NssSourceField,
                    MatchedText = m.Value,
                    FullFieldValue = LineTextAt(source, m.Index),
                    MatchOffset = m.Index - LineStartAt(source, m.Index),
                    MatchLength = m.Length,
                    Location = $"Line {LineNumberAt(source, m.Index)}"
                });
            }

            return new FileSearchResult
            {
                FilePath = filePath,
                ResourceType = resourceType,
                ToolId = toolId,
                Matches = matches
            };
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Failed to search {Path.GetFileName(filePath)}: {ex.Message}");

            return new FileSearchResult
            {
                FilePath = filePath,
                ResourceType = resourceType,
                ToolId = toolId,
                Matches = Array.Empty<SearchMatch>(),
                HadParseError = true,
                ParseError = ex.Message
            };
        }
    }

    private static int LineNumberAt(string text, int index)
    {
        int line = 1;
        for (int i = 0; i < index && i < text.Length; i++)
            if (text[i] == '\n') line++;
        return line;
    }

    private static int LineStartAt(string text, int index)
    {
        int start = text.LastIndexOf('\n', Math.Min(index, text.Length - 1));
        return start < 0 ? 0 : start + 1;
    }

    private static string LineTextAt(string text, int index)
    {
        int start = LineStartAt(text, index);
        int end = text.IndexOf('\n', start);
        if (end < 0) end = text.Length;
        return text.Substring(start, end - start).TrimEnd('\r');
    }

    /// <summary>
    /// Discover searchable files in a module directory.
    /// Applies FileTypeFilter at discovery time to skip irrelevant files.
    /// </summary>
    private static List<string> DiscoverFiles(string modulePath, IReadOnlyList<ushort>? fileTypeFilter)
    {
        var files = new List<string>();

        // Enumerate top-level files only (module resources are flat)
        foreach (var filePath in Directory.EnumerateFiles(modulePath))
        {
            var ext = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(ext))
                continue;

            if (!SearchableExtensions.Contains(ext))
                continue;

            // Apply file type filter if specified.
            // - null   = no filter, all types searched
            // - empty  = filter to nothing, no file-type search (filename pass may still run)
            // - non-empty = only these types searched
            if (fileTypeFilter != null)
            {
                var resourceType = ResourceTypes.FromExtension(ext);
                if (!fileTypeFilter.Contains(resourceType))
                    continue;
            }

            files.Add(filePath);
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    private static ushort GetResourceType(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return string.IsNullOrEmpty(ext) ? (ushort)0 : ResourceTypes.FromExtension(ext);
    }

    internal static string GetToolId(ushort resourceType)
    {
        return ToolIdMap.TryGetValue(resourceType, out var toolId) ? toolId : "";
    }
}
