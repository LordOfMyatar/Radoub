using Radoub.UI.Services;

namespace Radoub.UI.Views;

/// <summary>
/// Pure validation for the shared New-blueprint dialog (#2517). Extracted from the dialog
/// code-behind so the Name/Tag/ResRef rules are unit-testable without instantiating a Window.
/// Mirrors the rules previously duplicated in Fence's NewStoreWindow and Reliquary's
/// NewPlaceableWindow.
/// </summary>
public static class NewBlueprintValidation
{
    /// <summary>First validation error for the given field values, or null if all valid.</summary>
    public static string? Validate(string? name, string? tag, string? resRef)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        var tagValue = tag ?? string.Empty;
        var resRefValue = resRef ?? string.Empty;

        if (string.IsNullOrEmpty(trimmedName))
            return "Name is required.";

        // Tag is required and must be valid; an empty Tag is an error.
        if (!BlueprintNamingService.IsValidTag(tagValue))
            return "Tag must be 1-32 characters (A-Z, 0-9, underscore).";

        if (string.IsNullOrEmpty(resRefValue))
            return "ResRef is required.";

        if (!BlueprintNamingService.IsValidResRef(resRefValue))
            return "ResRef must be 1-16 lowercase alphanumeric/underscore characters.";

        return null;
    }
}
