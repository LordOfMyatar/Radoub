using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Resolver;

namespace RadoubLauncher.Services;

/// <summary>
/// Detects resource conflicts across a module's HAK files (#1162).
///
/// A conflict is a resource (same ResRef + same ResourceType) present in two or
/// more HAKs. The winner is the HAK earliest in the priority-ordered list:
/// BioWare's IFO spec states resources in earlier HAKs override later ones
/// (see Bioware-Legacy-IFO_Format.md and ModuleHakResolver, which both encode
/// "first = highest priority").
///
/// Detection only — this does not compare resource content, so identical
/// duplicates are still reported as conflicts.
/// </summary>
public static class HakConflictCheckerService
{
    /// <summary>
    /// Resolve a module's in-editor HAK list (names, in priority order) to file
    /// paths and check for conflicts. Names that cannot be found in the search
    /// paths are reported in <see cref="HakConflictReport.UnresolvedHaks"/> so the
    /// UI can warn that the check was incomplete.
    /// </summary>
    public static HakConflictReport CheckHakNames(
        IEnumerable<string> hakNamesInPriorityOrder, IEnumerable<string> hakSearchPaths)
    {
        var resolution = ModuleHakResolver.ResolveHakNames(hakNamesInPriorityOrder, hakSearchPaths);
        var conflicts = CheckHakPaths(resolution.Resolved);
        return new HakConflictReport(conflicts, resolution.Unresolved);
    }

    /// <summary>
    /// Read each HAK's resource list (metadata only — large HAKs are not loaded
    /// into memory) and detect cross-HAK conflicts. HAK paths must be supplied in
    /// priority order (first = highest priority), as returned by
    /// <see cref="ModuleHakResolver"/>.
    /// </summary>
    public static IReadOnlyList<HakConflict> CheckHakPaths(IEnumerable<string> hakPathsInPriorityOrder)
    {
        var contents = new List<HakContents>();
        foreach (var path in hakPathsInPriorityOrder)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var erf = ErfReader.ReadMetadataOnly(path);
            contents.Add(new HakContents(name, erf.Resources));
        }

        return FindConflicts(contents);
    }

    /// <summary>
    /// Pure conflict-detection core. Each HAK is a named resource list, supplied
    /// in priority order. Returns one <see cref="HakConflict"/> per resource that
    /// appears in two or more HAKs.
    /// </summary>
    public static IReadOnlyList<HakConflict> FindConflicts(IEnumerable<HakContents> haksInPriorityOrder)
    {
        var haks = haksInPriorityOrder.ToList();

        // Map each resource key to the ordered, de-duplicated list of HAKs that contain it.
        // Order is the order HAKs appear in the input (priority order).
        var byResource = new Dictionary<ResourceKey, List<string>>();

        foreach (var hak in haks)
        {
            // De-dup within a single HAK so a resource listed twice in one HAK
            // does not look like a cross-HAK conflict.
            var seenInThisHak = new HashSet<ResourceKey>();
            foreach (var entry in hak.Resources)
            {
                var key = new ResourceKey(entry.ResRef, entry.ResourceType);
                if (!seenInThisHak.Add(key))
                    continue;

                if (!byResource.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    byResource[key] = list;
                }
                list.Add(hak.Name);
            }
        }

        var conflicts = new List<HakConflict>();
        foreach (var (key, containingHaks) in byResource)
        {
            if (containingHaks.Count < 2)
                continue;

            conflicts.Add(new HakConflict(
                ResRef: key.ResRef,
                ResourceType: key.ResourceType,
                Extension: ResourceTypes.GetExtension(key.ResourceType),
                ContainingHaks: containingHaks,
                WinnerHak: containingHaks[0]));
        }

        return conflicts;
    }

    /// <summary>Case-insensitive resource identity: ResRef + ResourceType.</summary>
    private readonly record struct ResourceKey
    {
        public string ResRef { get; }
        public ushort ResourceType { get; }

        public ResourceKey(string resRef, ushort resourceType)
        {
            ResRef = resRef;
            ResourceType = resourceType;
        }

        public bool Equals(ResourceKey other) =>
            ResourceType == other.ResourceType &&
            string.Equals(ResRef, other.ResRef, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() =>
            HashCode.Combine(ResRef.ToLowerInvariant(), ResourceType);
    }
}

/// <summary>A HAK's resource list, identified by name (filename without extension).</summary>
public sealed record HakContents(string Name, IReadOnlyList<ErfResourceEntry> Resources);

/// <summary>
/// Result of a conflict check over a module's HAK list: the detected conflicts
/// plus any HAK names that could not be resolved to files (so the check was
/// incomplete for those).
/// </summary>
public sealed record HakConflictReport(
    IReadOnlyList<HakConflict> Conflicts,
    IReadOnlyList<string> UnresolvedHaks);

/// <summary>
/// A single cross-HAK conflict: one resource present in multiple HAKs.
/// <see cref="ContainingHaks"/> is in priority order; <see cref="WinnerHak"/> is the first.
/// </summary>
public sealed record HakConflict(
    string ResRef,
    ushort ResourceType,
    string Extension,
    IReadOnlyList<string> ContainingHaks,
    string WinnerHak);
