using Radoub.Formats.Common;
using Radoub.Formats.Gff;

namespace Radoub.Formats.Search.Rename;

/// <summary>
/// Pure logic: given a parsed GFF and an old ResRef, returns every
/// reference site to that ResRef inside the file. No I/O.
/// See spec Section 4 (NonPublic/Trebuchet/2026-05-03-resref-rename-design.md).
///
/// Top-level scalar ResRef fields are discovered via SearchFieldRegistry.
/// Nested structures (GIT instance lists, UTC equipment/inventory, UTM store
/// panels, IFO HAK list, DLG node walks) are handled by per-resource-type
/// methods because they require list traversal that the registry doesn't model.
/// </summary>
public class ResRefReferenceScanner
{
    private readonly SearchFieldRegistry _registry;

    public ResRefReferenceScanner(SearchFieldRegistry? registry = null)
    {
        _registry = registry ?? CreateDefault();
    }

    private static SearchFieldRegistry CreateDefault()
    {
        var registry = new SearchFieldRegistry();
        FieldRegistrations.RegisterAll(registry);
        return registry;
    }

    public IReadOnlyList<ResRefReference> Scan(
        GffFile gffFile,
        ushort resourceType,
        string oldResRef,
        string filePath)
    {
        var results = new List<ResRefReference>();

        ScanTopLevelScalarFields(gffFile, resourceType, oldResRef, filePath, results);
        // Per-type nested handlers added in subsequent tasks

        return results;
    }

    private void ScanTopLevelScalarFields(
        GffFile gff, ushort resourceType, string oldResRef, string filePath,
        List<ResRefReference> results)
    {
        var fields = _registry.GetSearchableFields(resourceType);

        foreach (var field in fields)
        {
            // ResRef-carrier types: ResRef and Script (scripts are .nss/.ncs ResRefs)
            if (field.FieldType != SearchFieldType.ResRef && field.FieldType != SearchFieldType.Script)
                continue;

            // Top-level lookup only. Nested traversal is per-type below.
            var gffField = gff.RootStruct.GetField(field.GffPath);
            if (gffField?.Value is string value
                && string.Equals(value, oldResRef, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new ResRefReference
                {
                    FilePath = filePath,
                    ResourceType = resourceType,
                    Field = field,
                    Location = field.Name,
                    OldValue = value,
                    NewValue = string.Empty,  // orchestrator fills NewValue at plan-build time
                    ScopeTier = ResRefScopeTier.TypedGffField
                });
            }
        }
    }
}
