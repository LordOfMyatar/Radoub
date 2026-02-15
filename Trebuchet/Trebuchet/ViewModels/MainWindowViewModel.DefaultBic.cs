using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

namespace RadoubLauncher.ViewModels;

// DefaultBic reading, scanning, saving, and partial change handlers
public partial class MainWindowViewModel
{
    /// <summary>
    /// Read the DefaultBic value from the current module's IFO file and scan for available BIC files.
    /// Called when module changes to correctly enable/disable Load Module button.
    /// </summary>
    private async Task ReadModuleDefaultBicAsync(CancellationToken cancellationToken = default)
    {
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (string.IsNullOrEmpty(modulePath))
        {
            RadoubSettings.Instance.CurrentModuleDefaultBic = string.Empty;
            AvailableBicFiles.Clear();
            UseDefaultBic = false;
            SelectedDefaultBic = string.Empty;
            OnPropertyChanged(nameof(HasBicFilesAvailable));
            OnPropertyChanged(nameof(NoBicFilesMessage));
            OnPropertyChanged(nameof(IsDefaultBicDropdownEnabled));
            return;
        }

        try
        {
            var (defaultBic, bicFiles) = await Task.Run(() => ReadDefaultBicAndScanFiles(modulePath), cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Update available BIC files
            AvailableBicFiles.Clear();
            foreach (var bic in bicFiles)
            {
                AvailableBicFiles.Add(bic);
            }

            // Set the current DefaultBic state
            RadoubSettings.Instance.CurrentModuleDefaultBic = defaultBic;

            // Find matching BIC in available files (case-insensitive)
            if (!string.IsNullOrEmpty(defaultBic))
            {
                var matchingBic = AvailableBicFiles.FirstOrDefault(
                    b => string.Equals(b, defaultBic, StringComparison.OrdinalIgnoreCase));
                SelectedDefaultBic = matchingBic ?? defaultBic;
                UseDefaultBic = true;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Module uses DefaultBic: {defaultBic}");
            }
            else
            {
                SelectedDefaultBic = string.Empty;
                UseDefaultBic = false;
            }

            OnPropertyChanged(nameof(HasBicFilesAvailable));
            OnPropertyChanged(nameof(NoBicFilesMessage));
            OnPropertyChanged(nameof(IsDefaultBicDropdownEnabled));
            OnPropertyChanged(nameof(LoadModuleTooltip));
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "DefaultBic read cancelled");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not read DefaultBic from module: {ex.Message}");
            RadoubSettings.Instance.CurrentModuleDefaultBic = string.Empty;
            AvailableBicFiles.Clear();
            UseDefaultBic = false;
            SelectedDefaultBic = string.Empty;
            OnPropertyChanged(nameof(HasBicFilesAvailable));
            OnPropertyChanged(nameof(NoBicFilesMessage));
            OnPropertyChanged(nameof(IsDefaultBicDropdownEnabled));
        }
    }

    /// <summary>
    /// Read the DefaultBic value and scan for available BIC files from a module.
    /// </summary>
    private static (string DefaultBic, List<string> BicFiles) ReadDefaultBicAndScanFiles(string modulePath)
    {
        var bicFiles = new List<string>();
        string defaultBic = string.Empty;

        string? workingDir = null;

        if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
        {
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            var moduleDir = Path.GetDirectoryName(modulePath);
            if (!string.IsNullOrEmpty(moduleDir))
            {
                workingDir = Path.Combine(moduleDir, moduleName);
            }
        }
        else if (Directory.Exists(modulePath))
        {
            workingDir = modulePath;
        }

        // Scan for BIC files in working directory
        if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
        {
            try
            {
                var files = Directory.GetFiles(workingDir, "*.bic", SearchOption.TopDirectoryOnly);
                bicFiles.AddRange(files.Select(f => Path.GetFileNameWithoutExtension(f)).OrderBy(f => f));
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"BIC scan: Found {bicFiles.Count} files in {UnifiedLogger.SanitizePath(workingDir)}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"BIC scan failed: {ex.Message}");
            }
        }
        else
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"BIC scan: Working dir not found. workingDir={workingDir ?? "null"}, exists={!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir)}");
        }

        // Read DefaultBic from IFO
        defaultBic = ReadDefaultBicFromModule(modulePath);

        return (defaultBic, bicFiles);
    }

    /// <summary>
    /// Read the DefaultBic value from a module's IFO file.
    /// </summary>
    private static string ReadDefaultBicFromModule(string modulePath)
    {
        Radoub.Formats.Ifo.IfoFile? ifoFile = null;

        if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
        {
            // Check for unpacked working directory first
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            var moduleDir = Path.GetDirectoryName(modulePath);
            if (!string.IsNullOrEmpty(moduleDir))
            {
                var workingDir = Path.Combine(moduleDir, moduleName);
                var ifoPath = Path.Combine(workingDir, "module.ifo");
                if (File.Exists(ifoPath))
                {
                    ifoFile = Radoub.Formats.Ifo.IfoReader.Read(ifoPath);
                }
            }

            // Fall back to reading from .mod file
            if (ifoFile == null)
            {
                var erf = ErfReader.ReadMetadataOnly(modulePath);
                var ifoEntry = erf.FindResource("module", ResourceTypes.Ifo);
                if (ifoEntry != null)
                {
                    var ifoData = ErfReader.ExtractResource(modulePath, ifoEntry);
                    ifoFile = Radoub.Formats.Ifo.IfoReader.Read(ifoData);
                }
            }
        }
        else if (Directory.Exists(modulePath))
        {
            var ifoPath = Path.Combine(modulePath, "module.ifo");
            if (File.Exists(ifoPath))
            {
                ifoFile = Radoub.Formats.Ifo.IfoReader.Read(ifoPath);
            }
        }

        return ifoFile?.DefaultBic ?? string.Empty;
    }

    // Partial methods for DefaultBic property changes

    partial void OnUseDefaultBicChanged(bool value)
    {
        OnPropertyChanged(nameof(CanLoadModule));
        OnPropertyChanged(nameof(LoadModuleTooltip));
        OnPropertyChanged(nameof(IsDefaultBicDropdownEnabled));

        if (!value)
        {
            // Clear DefaultBic when unchecked
            SelectedDefaultBic = string.Empty;
            _ = SaveDefaultBicToModuleAsync(string.Empty, _cts.Token);
        }
        else if (AvailableBicFiles.Count > 0 && string.IsNullOrEmpty(SelectedDefaultBic))
        {
            // Auto-select first BIC if available
            SelectedDefaultBic = AvailableBicFiles[0];
        }
    }

    partial void OnSelectedDefaultBicChanged(string value)
    {
        // Save to module IFO when selection changes
        if (UseDefaultBic && !string.IsNullOrEmpty(value))
        {
            _ = SaveDefaultBicToModuleAsync(value, _cts.Token);
        }
    }

    /// <summary>
    /// Save the DefaultBic value to the current module's IFO file.
    /// </summary>
    private async Task SaveDefaultBicToModuleAsync(string defaultBic, CancellationToken cancellationToken = default)
    {
        var workingDir = GetWorkingDirectoryPath();
        if (string.IsNullOrEmpty(workingDir))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot save DefaultBic: no working directory");
            return;
        }

        var ifoPath = Path.Combine(workingDir, "module.ifo");
        if (!File.Exists(ifoPath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot save DefaultBic: module.ifo not found");
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                var ifoFile = Radoub.Formats.Ifo.IfoReader.Read(ifoPath);
                ifoFile.DefaultBic = defaultBic;
                Radoub.Formats.Ifo.IfoWriter.Write(ifoFile, ifoPath);
            }, cancellationToken);

            // Update RadoubSettings
            RadoubSettings.Instance.CurrentModuleDefaultBic = defaultBic;
            OnPropertyChanged(nameof(CanLoadModule));
            OnPropertyChanged(nameof(LoadModuleTooltip));

            // IFO was modified - mark module dirty so user knows to rebuild
            IsModuleDirty = true;

            if (!string.IsNullOrEmpty(defaultBic))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Saved DefaultBic: {defaultBic}");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "Cleared DefaultBic");
            }
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "DefaultBic save cancelled");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save DefaultBic: {ex.Message}");
        }
    }
}
