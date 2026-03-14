using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Ifo;
using Radoub.Formats.Logging;

namespace RadoubLauncher.ViewModels;

// Save, Unpack, and module persistence operations
public partial class ModuleEditorViewModel
{
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_ifoFile == null || string.IsNullOrEmpty(_modulePath))
        {
            StatusText = "No module to save";
            return;
        }

        IsLoading = true;
        StatusText = "Saving...";

        try
        {
            UpdateIfoFromViewModel();

            await Task.Run(() =>
            {
                if (_isFromModFile && !string.IsNullOrEmpty(_modFilePath))
                {
                    // Save to MOD file
                    var ifoData = IfoWriter.Write(_ifoFile);
                    var backupPath = ErfWriter.UpdateResource(_modFilePath, "module", ResourceTypes.Ifo, ifoData, createBackup: true);

                    if (backupPath != null)
                    {
                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Created backup: {UnifiedLogger.SanitizePath(backupPath)}");
                    }
                }
                else
                {
                    // Save to extracted directory
                    var ifoPath = Path.Combine(_modulePath!, "module.ifo");
                    IfoWriter.Write(_ifoFile, ifoPath);
                }
            });

            StatusText = _isFromModFile ? "Saved to MOD file" : "Saved module.ifo";
            HasUnsavedChanges = false;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Saved IFO for: {UnifiedLogger.SanitizePath(_modulePath)}");
        }
        catch (IOException ioEx) when (IsFileLockException(ioEx))
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Save failed: file locked - {ioEx.Message}");
            StatusText = "Save failed: module.ifo is locked by another process";
            ShowFileLockWarning(
                "module.ifo is locked by another process and cannot be saved.\n\n"
                + "This usually means Aurora Toolset has the module open "
                + "and is holding a lock on the file.\n\n"
                + "To fix this:\n"
                + "  1. Close the module in Aurora Toolset\n"
                + "  2. Try saving again in Trebuchet\n\n"
                + $"Details: {ioEx.Message}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save: {ex.Message}");
            StatusText = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Unpack Command

    [RelayCommand(CanExecute = nameof(CanUnpack))]
    private async Task UnpackAsync()
    {
        if (string.IsNullOrEmpty(_modFilePath))
        {
            StatusText = "No MOD file to unpack";
            return;
        }

        // Default unpack directory: same folder as .mod, with module name
        var moduleName = Path.GetFileNameWithoutExtension(_modFilePath);
        var moduleDir = Path.GetDirectoryName(_modFilePath);
        var targetDir = Path.Combine(moduleDir!, moduleName);

        // Check if directory already exists
        if (Directory.Exists(targetDir))
        {
            // For now, we'll skip if exists - future: could prompt user
            StatusText = $"Directory already exists: {moduleName}/";
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Unpack target already exists: {UnifiedLogger.SanitizePath(targetDir)}");

            // Reload from unpacked directory to switch to editable mode
            await LoadModuleAsync(_modFilePath);
            return;
        }

        IsLoading = true;
        StatusText = "Unpacking module...";

        try
        {
            var resourceCount = await Task.Run(() => UnpackModuleToDirectory(_modFilePath, targetDir));

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Unpacked {resourceCount} resources to {UnifiedLogger.SanitizePath(targetDir)}");

            // Reload module from unpacked directory (now editable)
            StatusText = $"Unpacked {resourceCount} files. Reloading...";
            await LoadModuleAsync(_modFilePath);

            StatusText = $"Unpacked to {moduleName}/ - Now editable";
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Unpack failed: {ex.Message}");
            StatusText = $"Unpack failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Extract all resources from a MOD file to a directory.
    /// </summary>
    private static int UnpackModuleToDirectory(string modFilePath, string targetDir)
    {
        // Create target directory
        Directory.CreateDirectory(targetDir);

        // Read ERF metadata (doesn't load resource data into memory)
        var erf = ErfReader.ReadMetadataOnly(modFilePath);

        var count = 0;
        foreach (var resource in erf.Resources)
        {
            // Get file extension for this resource type
            var extension = ResourceTypes.GetExtension(resource.ResourceType);
            var fileName = $"{resource.ResRef}{extension}";
            var filePath = Path.Combine(targetDir, fileName);

            // Extract resource data from MOD file
            var data = ErfReader.ExtractResource(modFilePath, resource);

            // Write to file
            File.WriteAllBytes(filePath, data);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Check if an IOException is caused by a file sharing violation (file locked by another process).
    /// </summary>
    private static bool IsFileLockException(IOException ex)
    {
        const int sharingViolation = unchecked((int)0x80070020);
        const int lockViolation = unchecked((int)0x80070021);
        return ex.HResult == sharingViolation || ex.HResult == lockViolation;
    }

    /// <summary>
    /// Show a prominent warning dialog when a file is locked by another process.
    /// </summary>
    private void ShowFileLockWarning(string message)
    {
        if (_parentWindow == null) return;

        var dialog = new Views.AlertDialog("File Locked", message);
        dialog.Show(_parentWindow);
    }
}
