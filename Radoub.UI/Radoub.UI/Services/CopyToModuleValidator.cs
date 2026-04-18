using System;
using System.IO;

namespace Radoub.UI.Services;

/// <summary>
/// Result of a Copy-to-Module dialog. Null values for Tag/Name
/// indicate the dialog was configured to hide those fields (e.g., Parley).
/// </summary>
public sealed record CopyToModuleResult(string NewResRef, string? NewTag, string? NewName);

/// <summary>
/// Validation state for the Copy-to-Module dialog's editable fields.
/// </summary>
public enum CopyToModuleValidationState
{
    Valid,
    EmptyResRef,
    InvalidResRef,
    DuplicateFile,
    TagTooLong,
    Unchanged
}

public sealed record CopyToModuleValidationResult(
    CopyToModuleValidationState State,
    string? Message)
{
    public bool IsValid => State == CopyToModuleValidationState.Valid;
}

/// <summary>
/// Validates Copy-to-Module dialog inputs:
/// ResRef (Aurora filename rules + duplicate-in-module check),
/// Tag (NWN 32-char limit), Name (any non-empty string).
/// </summary>
public static class CopyToModuleValidator
{
    /// <summary>NWN Tag field maximum length.</summary>
    public const int MaxTagLength = 32;

    /// <summary>
    /// Validate the dialog's current state against target module directory and extension.
    /// </summary>
    /// <param name="resRef">New ResRef (filename stem)</param>
    /// <param name="tag">New Tag (ignored when showTagAndName is false)</param>
    /// <param name="originalResRef">Original ResRef — used to detect "unchanged"</param>
    /// <param name="moduleDirectory">Directory where the copy will be written</param>
    /// <param name="extension">File extension including the dot (e.g. ".utm")</param>
    /// <param name="showTagAndName">Whether Tag/Name are being edited (Parley: false)</param>
    public static CopyToModuleValidationResult Validate(
        string resRef,
        string? tag,
        string originalResRef,
        string moduleDirectory,
        string extension,
        bool showTagAndName)
    {
        var trimmed = resRef?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(trimmed))
        {
            return new CopyToModuleValidationResult(
                CopyToModuleValidationState.EmptyResRef,
                "ResRef is required.");
        }

        var filenameResult = AuroraFilenameValidator.Validate(trimmed);
        if (!filenameResult.IsValid)
        {
            return new CopyToModuleValidationResult(
                CopyToModuleValidationState.InvalidResRef,
                filenameResult.GetErrorMessage());
        }

        if (showTagAndName && tag != null && tag.Length > MaxTagLength)
        {
            return new CopyToModuleValidationResult(
                CopyToModuleValidationState.TagTooLong,
                $"Tag is too long ({tag.Length} characters). Maximum is {MaxTagLength}.");
        }

        // Duplicate-file check only fires when we have a real directory to check.
        // Tests that pass null/empty directory skip this branch.
        if (!string.IsNullOrEmpty(moduleDirectory))
        {
            var destPath = Path.Combine(moduleDirectory, trimmed + extension);
            if (File.Exists(destPath))
            {
                return new CopyToModuleValidationResult(
                    CopyToModuleValidationState.DuplicateFile,
                    $"A file named \"{trimmed}{extension}\" already exists in this directory.");
            }
        }

        // Unchanged is only a soft state — the copy should still be allowed as long as
        // a duplicate check didn't fire (same resref, different target dir is fine).
        if (string.Equals(trimmed, originalResRef, StringComparison.OrdinalIgnoreCase))
        {
            return new CopyToModuleValidationResult(
                CopyToModuleValidationState.Unchanged,
                "ResRef is unchanged from the source.");
        }

        return new CopyToModuleValidationResult(CopyToModuleValidationState.Valid, null);
    }
}
