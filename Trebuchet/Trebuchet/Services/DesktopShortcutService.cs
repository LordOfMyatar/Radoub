using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text;

namespace RadoubLauncher.Services;

/// <summary>
/// Creates a desktop shortcut for Trebuchet (#1437). Windows produces a .lnk via
/// the WScript.Shell COM object; Linux writes a freedesktop .desktop file. The
/// content/path construction is pure and unit-tested; the actual COM call and
/// filesystem writes are platform side effects.
/// </summary>
public static class DesktopShortcutService
{
    private const string LinuxCategories = "Utility;Development;";

    /// <summary>
    /// Build a freedesktop .desktop entry. <paramref name="iconPath"/> may be
    /// empty to omit the Icon line. Exec/Icon paths containing spaces are quoted.
    /// </summary>
    public static string BuildLinuxDesktopEntry(string name, string execPath, string iconPath, string comment)
    {
        var sb = new StringBuilder();
        sb.Append("[Desktop Entry]\n");
        sb.Append("Type=Application\n");
        sb.Append($"Name={name}\n");
        if (!string.IsNullOrEmpty(comment))
            sb.Append($"Comment={comment}\n");
        sb.Append($"Exec={QuoteIfNeeded(execPath)}\n");
        if (!string.IsNullOrEmpty(iconPath))
            sb.Append($"Icon={QuoteIfNeeded(iconPath)}\n");
        sb.Append("Terminal=false\n");
        sb.Append($"Categories={LinuxCategories}\n");
        return sb.ToString();
    }

    /// <summary>Path of the .desktop file: lowercase, dashed name in the applications dir.</summary>
    public static string GetLinuxDesktopFilePath(string applicationsDir, string name)
    {
        var fileName = Sanitize(name) + ".desktop";
        return Path.Combine(applicationsDir, fileName);
    }

    /// <summary>Path of the Windows .lnk file in the given directory (usually the Desktop).</summary>
    public static string GetWindowsShortcutPath(string desktopDir, string name)
    {
        return Path.Combine(desktopDir, name + ".lnk");
    }

    /// <summary>
    /// Create a desktop shortcut for the running Trebuchet executable. Idempotent —
    /// re-running overwrites any existing shortcut. Returns a result describing the
    /// outcome and the shortcut path (or the error).
    /// </summary>
    public static ShortcutResult CreateForCurrentApp(string? iconPath = null)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            return ShortcutResult.Fail("Could not determine the Trebuchet executable path.");

        try
        {
            if (OperatingSystem.IsWindows())
                return CreateWindowsShortcut(exePath, iconPath);
            if (OperatingSystem.IsLinux())
                return CreateLinuxShortcut(exePath, iconPath);

            return ShortcutResult.Fail("Desktop shortcuts are only supported on Windows and Linux.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ShortcutResult.Fail($"Failed to create shortcut: {ex.Message}");
        }
    }

    [SupportedOSPlatform("linux")]
    private static ShortcutResult CreateLinuxShortcut(string exePath, string? iconPath)
    {
        var appsDir = GetXdgDataApplications();
        Directory.CreateDirectory(appsDir);

        var path = GetLinuxDesktopFilePath(appsDir, "Trebuchet");
        var entry = BuildLinuxDesktopEntry("Trebuchet", exePath, iconPath ?? string.Empty, "Radoub toolset launcher");
        File.WriteAllText(path, entry);

        // Mark executable where the platform supports it (best-effort; ignored on FS without modes).
        try { File.SetUnixFileMode(path, UnixFileModeAllReadExecuteOwnerWrite()); }
        catch (PlatformNotSupportedException) { /* not Unix — fine */ }

        return ShortcutResult.Ok(path);
    }

    [SupportedOSPlatform("windows")]
    private static ShortcutResult CreateWindowsShortcut(string exePath, string? iconPath)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var path = GetWindowsShortcutPath(desktop, "Trebuchet");

        // Late-bound WScript.Shell COM — avoids a hard Windows-only reference and
        // works without adding interop assemblies.
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell is unavailable.");
        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Could not create WScript.Shell.");

        dynamic shortcut = shell.CreateShortcut(path);
        shortcut.TargetPath = exePath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;
        shortcut.Description = "Radoub toolset launcher";
        if (!string.IsNullOrEmpty(iconPath))
            shortcut.IconLocation = iconPath;
        shortcut.Save();

        return ShortcutResult.Ok(path);
    }

    private static string GetXdgDataApplications()
    {
        // $XDG_DATA_HOME/applications, defaulting to ~/.local/share/applications.
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var dataHome = string.IsNullOrEmpty(xdg)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
            : xdg;
        return Path.Combine(dataHome, "applications");
    }

    private static UnixFileMode UnixFileModeAllReadExecuteOwnerWrite() =>
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private static string QuoteIfNeeded(string value) =>
        value.Contains(' ') ? $"\"{value}\"" : value;

    private static string Sanitize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        return sb.ToString();
    }
}

/// <summary>Outcome of a shortcut-creation attempt.</summary>
public sealed record ShortcutResult(bool Success, string? Path, string? Error)
{
    public static ShortcutResult Ok(string path) => new(true, path, null);
    public static ShortcutResult Fail(string error) => new(false, null, error);
}
