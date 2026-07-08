using System.IO;
using System.Text.RegularExpressions;
namespace Radoub.UI.Services;

/// <summary>
/// Pure path composition, Aurora filename validation, overwrite detection, and
/// directory resolution for the shared SaveBlueprintWindow (#2515). No UI or
/// Avalonia dependencies so the logic is unit-testable in isolation.
/// </summary>
public static class SaveBlueprintPathResolver
{
    private static readonly Regex AuroraSafe = new("^[A-Za-z0-9_]{1,16}$", RegexOptions.Compiled);

    public static bool IsValidAuroraFilename(string? name)
        => !string.IsNullOrEmpty(name) && AuroraSafe.IsMatch(name);

    public static string ComposePath(string directory, string resRef, string extension)
        => Path.Combine(directory, $"{resRef}.{extension}");

    public static bool WouldOverwrite(string fullPath) => File.Exists(fullPath);

    public static string? ResolveDirectory(string? overridePath, string? contextDir)
        => !string.IsNullOrEmpty(overridePath) ? overridePath
         : !string.IsNullOrEmpty(contextDir) ? contextDir : null;
}
