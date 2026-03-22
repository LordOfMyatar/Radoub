namespace Radoub.Formats.Search;

/// <summary>
/// Central registry of searchable fields per GFF file type.
/// Initialized at startup; read-only during search (thread-safe).
/// </summary>
public class SearchFieldRegistry
{
    private readonly Dictionary<ushort, List<FieldDefinition>> _fieldsByType = new();

    /// <summary>
    /// Register searchable fields for a file type.
    /// </summary>
    public void RegisterFileType(ushort resourceType, params FieldDefinition[] fields)
    {
        if (!_fieldsByType.ContainsKey(resourceType))
            _fieldsByType[resourceType] = new List<FieldDefinition>();

        _fieldsByType[resourceType].AddRange(fields);
    }

    /// <summary>
    /// Get all searchable fields for a file type.
    /// Returns empty list if type not registered.
    /// </summary>
    public IReadOnlyList<FieldDefinition> GetSearchableFields(ushort resourceType)
    {
        return _fieldsByType.TryGetValue(resourceType, out var fields)
            ? fields.AsReadOnly()
            : Array.Empty<FieldDefinition>();
    }

    /// <summary>
    /// Get fields filtered by category for a file type.
    /// </summary>
    public IReadOnlyList<FieldDefinition> GetFieldsByCategory(
        ushort resourceType, SearchFieldCategory category)
    {
        return GetSearchableFields(resourceType)
            .Where(f => f.Category == category)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Get all registered file types.
    /// </summary>
    public IReadOnlyList<ushort> GetAllFileTypes()
    {
        return _fieldsByType.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// Check if a field supports replace for a given file type.
    /// </summary>
    public bool IsReplaceable(ushort resourceType, string fieldName)
    {
        return GetSearchableFields(resourceType)
            .Any(f => f.Name == fieldName && f.IsReplaceable);
    }

    /// <summary>
    /// Returns true if the file type has any registered fields.
    /// </summary>
    public bool IsRegistered(ushort resourceType)
    {
        return _fieldsByType.ContainsKey(resourceType);
    }
}
