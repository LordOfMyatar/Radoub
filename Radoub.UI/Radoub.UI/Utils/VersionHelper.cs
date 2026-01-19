using System.Reflection;

namespace Radoub.UI.Utils;

/// <summary>
/// Provides version information for applications using assembly metadata.
/// Centralized implementation to avoid code duplication across tools.
/// </summary>
public static class VersionHelper
{
    /// <summary>
    /// Gets the semantic version string for the specified assembly.
    /// Uses InformationalVersion if available, stripping any git hash suffix.
    /// Falls back to AssemblyVersion if InformationalVersion is not set.
    /// </summary>
    /// <param name="assembly">The assembly to get version for. If null, uses entry assembly (main executable).</param>
    /// <returns>Version string (e.g., "0.1.0-alpha")</returns>
    public static string GetVersion(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetEntryAssembly();
        if (assembly == null)
            return "1.0.0";

        try
        {
            var infoVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (!string.IsNullOrEmpty(infoVersion))
            {
                // InformationalVersion may include commit hash (e.g., "0.1.0-alpha+abc123")
                // Strip everything after '+' to get clean version
                var plusIndex = infoVersion.IndexOf('+');
                if (plusIndex > 0)
                    infoVersion = infoVersion[..plusIndex];
                return infoVersion;
            }

            // Fallback to AssemblyVersion
            var version = assembly.GetName().Version;
            if (version != null)
                return $"{version.Major}.{version.Minor}.{version.Build}";
        }
        catch
        {
            // Fallback if metadata unavailable
        }

        return "1.0.0";
    }

    /// <summary>
    /// Gets the semantic version string for the entry assembly (main application).
    /// </summary>
    /// <returns>Version string (e.g., "0.1.0-alpha")</returns>
    public static string GetEntryAssemblyVersion()
    {
        return GetVersion(Assembly.GetEntryAssembly());
    }
}
