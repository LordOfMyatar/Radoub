using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using RadoubLauncher.Models;
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
                // "Compile all" = every compilable .nss in working dir; "Only changed" = stale scripts only
                // Both modes skip include/library files (no void main or StartingConditional)
                List<string> scriptsToCompile;
                if (SettingsService.Instance.BuildUncompiledScriptsEnabled)
                {
                    scriptsToCompile = Directory.GetFiles(workingDir, "*.nss", SearchOption.TopDirectoryOnly)
                        .Where(ScriptCompilerService.HasUncommentedEntryPoint)
                        .ToList();
                }
                else
                {
                    scriptsToCompile = staleScripts.Select(s => s.NssPath).ToList();
                }

                if (scriptsToCompile.Count > 0)
                {
                    BuildStatusText = $"Compiling {scriptsToCompile.Count} scripts...";

                    var compileResult = await compilerService.CompileScriptsAsync(
                        scriptsToCompile,
                        progress: (current, total, name) =>
                        {
                            BuildStatusText = $"Compiling {current}/{total}: {name}";
                        });

                    // Always write a log when compilation runs
                    var logPath = BuildLogService.WriteCompilationLog(compileResult, workingDir);
                    _lastBuildLogPath = logPath;
                    _lastBuildWorkingDir = workingDir;
                    HasBuildLog = true;

                    if (!compileResult.Success)
                    {
                        BuildStatusText = $"Build failed: {compileResult.FailedScripts.Count} script(s) failed";
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
            ClearFailedScripts();
            StaleScriptCount = 0;
            IsModuleDirty = false;
            HasNewerWorkingFiles = false;
            NewerFileCount = 0;
        }
        catch (IOException ioEx) when (IsFileLockException(ioEx))
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Build failed: file locked - {ioEx.Message}");
            BuildStatusText = "Build failed: file is locked by another process";
            ShowFileLockWarning(ioEx);
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
    private void OpenBuildLogFolder()
    {
        var logsDir = Path.Combine(Path.GetTempPath(), "Radoub", "BuildLogs");

        if (!Directory.Exists(logsDir))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Build log folder does not exist");
            return;
        }

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = logsDir,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo)?.Dispose();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open build log folder: {ex.Message}");
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
    private async Task RecompileSelectedScripts()
    {
        var selectedScripts = FailedScriptItems.Where(s => s.IsSelected).ToList();
        if (selectedScripts.Count == 0)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "No scripts selected to recompile");
            return;
        }

        var compilerService = ScriptCompilerService.Instance;
        if (!compilerService.IsCompilerAvailable)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Compiler not available");
            return;
        }

        IsBuilding = true;

        try
        {
            var scriptPaths = selectedScripts.Select(s => s.FullPath).ToList();
            BuildStatusText = $"Recompiling {scriptPaths.Count} scripts...";

            var compileResult = await compilerService.CompileScriptsAsync(
                scriptPaths,
                progress: (current, total, name) =>
                {
                    BuildStatusText = $"Recompiling {current}/{total}: {name}";
                });

            // Write updated log
            var workingDir = _lastBuildWorkingDir ?? GetWorkingDirectoryPath() ?? "";
            var logPath = BuildLogService.WriteCompilationLog(compileResult, workingDir);
            _lastBuildLogPath = logPath;
            _lastBuildWorkingDir = workingDir;
            HasBuildLog = true;

            // Remove successfully compiled scripts from the failed list
            var succeededPaths = compileResult.Results
                .Where(r => r.Success)
                .Select(r => r.ScriptPath)
                .ToHashSet();

            var itemsToRemove = FailedScriptItems
                .Where(item => succeededPaths.Contains(item.FullPath))
                .ToList();
            foreach (var item in itemsToRemove)
                FailedScriptItems.Remove(item);

            // Update error summaries for scripts that still failed
            foreach (var result in compileResult.Results.Where(r => !r.Success))
            {
                var existingItem = FailedScriptItems.FirstOrDefault(i => i.FullPath == result.ScriptPath);
                if (existingItem != null)
                {
                    existingItem.ErrorSummary = result.ErrorMessage?.Split('\n').FirstOrDefault()?.Trim() ?? "Compilation failed";
                }
            }

            HasFailedScripts = FailedScriptItems.Count > 0;

            if (!HasFailedScripts)
            {
                // All scripts compiled successfully — proceed to pack
                BuildStatusText = "All scripts compiled — packing module...";
                var modFilePath = GetModFilePath();

                if (!string.IsNullOrEmpty(workingDir) && !string.IsNullOrEmpty(modFilePath))
                {
                    var resourceCount = await Task.Run(() => PackDirectoryToMod(workingDir, modFilePath));
                    _lastBuildTimeUtc = DateTime.UtcNow;
                    BuildStatusText = $"Built {resourceCount} files to {Path.GetFileName(modFilePath)}";
                    StaleScriptCount = 0;
                    IsModuleDirty = false;
                    HasNewerWorkingFiles = false;
                    NewerFileCount = 0;

                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Recompile + pack: {resourceCount} resources to {UnifiedLogger.SanitizePath(modFilePath)}");
                }
            }
            else
            {
                BuildStatusText = $"Recompile: {succeededPaths.Count} succeeded, {FailedScriptItems.Count} still failing";
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Recompile partial: {succeededPaths.Count} fixed, {FailedScriptItems.Count} remaining");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Recompile failed: {ex.Message}");
            BuildStatusText = $"Recompile failed: {ex.Message}";
        }
        finally
        {
            IsBuilding = false;
        }
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

    /// <summary>
    /// Check if an IOException is caused by a file sharing violation (file locked by another process).
    /// Windows HRESULT 0x80070020 = ERROR_SHARING_VIOLATION.
    /// </summary>
    private static bool IsFileLockException(IOException ex)
    {
        // HResult 0x80070020 = ERROR_SHARING_VIOLATION (32)
        // HResult 0x80070021 = ERROR_LOCK_VIOLATION (33)
        const int sharingViolation = unchecked((int)0x80070020);
        const int lockViolation = unchecked((int)0x80070021);
        return ex.HResult == sharingViolation || ex.HResult == lockViolation;
    }

    /// <summary>
    /// Show a prominent warning dialog when a file is locked by another process.
    /// </summary>
    private void ShowFileLockWarning(IOException ex)
    {
        if (_parentWindow == null) return;

        var message = "A module file is locked by another process and cannot be written.\n\n"
            + "This usually means the Aurora Toolset (or another program) still has the .mod file open.\n\n"
            + "To fix this:\n"
            + "  1. Close the module in Aurora Toolset\n"
            + "  2. Try building again in Trebuchet\n\n"
            + $"Details: {ex.Message}";

        var dialog = new Views.AlertDialog("File Locked", message);
        dialog.Show(_parentWindow);
    }
}
