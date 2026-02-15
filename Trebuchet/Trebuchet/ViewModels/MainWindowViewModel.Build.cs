using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using RadoubLauncher.Services;

namespace RadoubLauncher.ViewModels;

// Build, compilation, packing, build log, failed scripts, and build status checks
public partial class MainWindowViewModel
{
    // Store path to last build log for "View Log" functionality
    private string? _lastBuildLogPath;
    private string? _lastBuildWorkingDir;

    // Timestamp of last successful build to prevent false-positive "newer files" warnings
    // caused by filesystem timestamp granularity races after packing the .mod file
    private DateTime? _lastBuildTimeUtc;

    [RelayCommand(CanExecute = nameof(CanBuildModule))]
    private async Task BuildModuleAsync()
    {
        var workingDir = GetWorkingDirectoryPath();
        var modFilePath = GetModFilePath();

        if (string.IsNullOrEmpty(workingDir) || string.IsNullOrEmpty(modFilePath))
        {
            BuildStatusText = "Cannot build: no working directory found";
            UnifiedLogger.LogApplication(LogLevel.WARN, "Build failed: no working directory");
            return;
        }

        IsBuilding = true;

        try
        {
            // Save module editor IFO changes first if dirty
            if (_moduleEditorViewModel?.HasUnsavedChanges == true)
            {
                BuildStatusText = "Saving IFO changes...";
                await _moduleEditorViewModel.SaveCommand.ExecuteAsync(null);
            }

            // Save faction editor changes if dirty
            if (_factionEditorViewModel?.HasUnsavedChanges == true)
            {
                BuildStatusText = "Saving faction changes...";
                _factionEditorViewModel.SaveCommand.Execute(null);
            }

            // Check for stale scripts (always check, regardless of compile setting)
            var compilerService = ScriptCompilerService.Instance;
            var staleScripts = compilerService.FindStaleScripts(workingDir);

            // If compile scripts is enabled and compiler is available, compile first
            if (SettingsService.Instance.CompileScriptsEnabled && compilerService.IsCompilerAvailable)
            {
                if (staleScripts.Count > 0)
                {
                    BuildStatusText = $"Compiling {staleScripts.Count} scripts...";

                    var compileResult = await compilerService.CompileAllScriptsAsync(
                        workingDir,
                        compileAll: false,
                        progress: (current, total, name) =>
                        {
                            BuildStatusText = $"Compiling {current}/{total}: {name}";
                        });

                    if (!compileResult.Success)
                    {
                        // Write log file for failed compilation
                        var logPath = compilerService.WriteCompilationLog(compileResult, workingDir);
                        BuildStatusText = $"Build failed: {compileResult.FailedScripts.Count} script(s) failed";
                        _lastBuildLogPath = logPath;
                        _lastBuildWorkingDir = workingDir;
                        HasBuildLog = true;
                        PopulateFailedScripts(compileResult);
                        UnifiedLogger.LogApplication(LogLevel.WARN,
                            $"Compilation failed for {compileResult.FailedScripts.Count} scripts");
                        return;
                    }

                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Compiled {compileResult.SuccessCount} scripts successfully");
                }
            }
            else if (staleScripts.Count > 0 && !SettingsService.Instance.CompileScriptsEnabled)
            {
                // Not compiling but there are stale scripts - warn user
                var staleCount = staleScripts.Count;
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Found {staleCount} scripts with outdated .ncs files (compilation disabled)");
            }

            // Pack the module
            BuildStatusText = "Packing module...";
            var resourceCount = await Task.Run(() => PackDirectoryToMod(workingDir, modFilePath));

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Built {resourceCount} resources to {UnifiedLogger.SanitizePath(modFilePath)}");

            _lastBuildTimeUtc = DateTime.UtcNow;
            BuildStatusText = $"Built {resourceCount} files to {Path.GetFileName(modFilePath)}";
            _lastBuildLogPath = null;
            _lastBuildWorkingDir = null;
            HasBuildLog = false;
            ClearFailedScripts();
            StaleScriptCount = 0;
            IsModuleDirty = false;
            HasNewerWorkingFiles = false;
            NewerFileCount = 0;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Build failed: {ex.Message}");
            BuildStatusText = $"Build failed: {ex.Message}";
        }
        finally
        {
            IsBuilding = false;
        }
    }

    [RelayCommand]
    private void OpenBuildLog()
    {
        if (string.IsNullOrEmpty(_lastBuildLogPath) || !File.Exists(_lastBuildLogPath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "No build log available");
            return;
        }

        try
        {
            // Open log file with default text editor
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _lastBuildLogPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo)?.Dispose();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open build log: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenFailedScriptsInEditor()
    {
        var selectedScripts = FailedScriptItems.Where(s => s.IsSelected).ToList();
        if (selectedScripts.Count == 0)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "No scripts selected to open");
            return;
        }

        var editorPath = SettingsService.Instance.CodeEditorPath;
        var useCustomEditor = !string.IsNullOrEmpty(editorPath) && File.Exists(editorPath);

        foreach (var script in selectedScripts)
        {
            try
            {
                var filePath = script.FullPath;
                if (!File.Exists(filePath))
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Script file not found: {Path.GetFileName(filePath)}");
                    continue;
                }

                System.Diagnostics.ProcessStartInfo startInfo;
                if (useCustomEditor)
                {
                    startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = editorPath,
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = false
                    };
                }
                else
                {
                    startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    };
                }

                System.Diagnostics.Process.Start(startInfo)?.Dispose();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open {script.ScriptName}: {ex.Message}");
            }
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Opened {selectedScripts.Count} script(s) in editor");
    }

    [RelayCommand]
    private void SelectAllFailedScripts()
    {
        foreach (var item in FailedScriptItems)
            item.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNoneFailedScripts()
    {
        foreach (var item in FailedScriptItems)
            item.IsSelected = false;
    }

    private void PopulateFailedScripts(BatchCompilationResult compileResult)
    {
        FailedScriptItems.Clear();
        foreach (var result in compileResult.Results.Where(r => !r.Success))
        {
            var scriptName = Path.GetFileName(result.ScriptPath);
            var errorSummary = result.ErrorMessage?.Split('\n').FirstOrDefault()?.Trim() ?? "Compilation failed";
            FailedScriptItems.Add(new FailedScriptItem
            {
                ScriptName = scriptName,
                FullPath = result.ScriptPath,
                ErrorSummary = errorSummary,
                IsSelected = true
            });
        }
        HasFailedScripts = FailedScriptItems.Count > 0;
    }

    private void ClearFailedScripts()
    {
        FailedScriptItems.Clear();
        HasFailedScripts = false;
    }

    /// <summary>
    /// Refresh all build-related checks: stale scripts and newer working files.
    /// Called on module load, after Module Editor closes, and before launch.
    /// </summary>
    public void RefreshBuildStatus()
    {
        CheckStaleScripts();
        CheckNewerWorkingFiles();
    }

    /// <summary>
    /// Check for stale scripts in the current module's working directory.
    /// </summary>
    public void CheckStaleScripts()
    {
        var workingDir = GetWorkingDirectoryPath();
        if (string.IsNullOrEmpty(workingDir))
        {
            StaleScriptCount = 0;
            return;
        }

        var staleScripts = ScriptCompilerService.Instance.FindStaleScripts(workingDir);
        StaleScriptCount = staleScripts.Count;
    }

    /// <summary>
    /// Check if any files in the working directory are newer than the .mod file.
    /// </summary>
    private void CheckNewerWorkingFiles()
    {
        var workingDir = GetWorkingDirectoryPath();
        var modPath = GetModFilePath();

        if (string.IsNullOrEmpty(workingDir) || string.IsNullOrEmpty(modPath) || !File.Exists(modPath))
        {
            HasNewerWorkingFiles = false;
            NewerFileCount = 0;
            return;
        }

        try
        {
            var modWriteTime = File.GetLastWriteTimeUtc(modPath);

            // Use the build completion timestamp if it's newer than the .mod file's
            // filesystem timestamp - prevents false positives from timestamp granularity
            if (_lastBuildTimeUtc.HasValue && _lastBuildTimeUtc.Value > modWriteTime)
                modWriteTime = _lastBuildTimeUtc.Value;

            var newerFiles = Directory.GetFiles(workingDir)
                .Count(f => File.GetLastWriteTimeUtc(f) > modWriteTime);

            NewerFileCount = newerFiles;
            HasNewerWorkingFiles = newerFiles > 0;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not check working directory timestamps: {ex.Message}");
            HasNewerWorkingFiles = false;
            NewerFileCount = 0;
        }
    }

    /// <summary>
    /// Pack a working directory into a .mod file.
    /// </summary>
    private static int PackDirectoryToMod(string workingDir, string modFilePath)
    {
        // Collect all files from working directory
        var files = Directory.GetFiles(workingDir);
        var resourceData = new Dictionary<(string ResRef, ushort Type), byte[]>();
        var resources = new List<ErfResourceEntry>();

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileName(filePath);
            var extension = Path.GetExtension(filePath);
            var resRef = Path.GetFileNameWithoutExtension(filePath);

            // Get resource type from extension
            var resourceType = ResourceTypes.FromExtension(extension);
            if (resourceType == ResourceTypes.Invalid)
            {
                // Skip unknown file types
                continue;
            }

            var data = File.ReadAllBytes(filePath);
            var key = (resRef.ToLowerInvariant(), resourceType);

            resourceData[key] = data;
            resources.Add(new ErfResourceEntry
            {
                ResRef = resRef,
                ResourceType = resourceType,
                ResId = (uint)resources.Count
            });
        }

        // Create ERF structure
        var erf = new ErfFile
        {
            FileType = "MOD ",
            FileVersion = "V1.0",
            BuildYear = (uint)(DateTime.Now.Year - 1900),
            BuildDay = (uint)DateTime.Now.DayOfYear
        };
        erf.Resources.AddRange(resources);

        // Write to .mod file
        ErfWriter.Write(erf, modFilePath, resourceData);

        return resources.Count;
    }
}
