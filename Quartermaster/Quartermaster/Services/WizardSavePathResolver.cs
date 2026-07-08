using System.IO;

namespace Quartermaster.Services;

public static class WizardSavePathResolver
{
    /// <summary>UTC → module working dir (or .mod parent); BIC → localvault. Pure; testable (#2515).</summary>
    public static string? ResolveDefaultDir(bool isBic, string? currentModulePath, string? nwnPath)
    {
        if (isBic)
            return string.IsNullOrEmpty(nwnPath) ? null : Path.Combine(nwnPath, "localvault");
        if (string.IsNullOrEmpty(currentModulePath)) return null;
        if (Directory.Exists(currentModulePath)) return currentModulePath;
        if (File.Exists(currentModulePath)) return Path.GetDirectoryName(currentModulePath);
        return null;
    }
}
