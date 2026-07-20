using System.Collections.Generic;
namespace Radoub.UI.Services;

/// <summary>Inputs for the shared SaveBlueprintWindow (#2515).</summary>
public sealed record SaveBlueprintOptions(
    string Title,
    IReadOnlyList<string> Extensions, // first is default; e.g. ["utm"] or ["utc","bic"]
    string DefaultResRef,
    IScriptBrowserContext? Context,
    IReadOnlyDictionary<string, string?>? DefaultDirectoryByExtension = null); // per-ext default dir (#2515)

/// <summary>Result of a confirmed save; null when cancelled.</summary>
public sealed record SaveBlueprintResult(string Path, string Extension);
