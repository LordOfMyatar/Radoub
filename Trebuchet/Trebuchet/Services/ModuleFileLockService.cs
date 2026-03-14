using System;
using System.IO;

namespace RadoubLauncher.Services;

/// <summary>
/// Checks whether a module file (.mod) is locked by another process.
/// Used to detect when Aurora Toolset holds a lock that would prevent
/// Trebuchet from packing/saving to the .mod file.
/// </summary>
public static class ModuleFileLockService
{
    /// <summary>
    /// Check if a file is locked for writing by another process.
    /// Attempts to open the file with write access; if it fails with a
    /// sharing violation, the file is considered locked.
    /// </summary>
    public static bool IsFileLocked(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            return false;
        }
        catch (IOException ex) when (IsSharingViolation(ex))
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSharingViolation(IOException ex)
    {
        const int sharingViolation = unchecked((int)0x80070020);
        const int lockViolation = unchecked((int)0x80070021);
        return ex.HResult == sharingViolation || ex.HResult == lockViolation;
    }
}
