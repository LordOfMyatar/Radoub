using System;
using System.IO;
using System.Threading.Tasks;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Ifo;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Avalonia.Controls;

namespace RadoubLauncher.ViewModels;

// Module loading, file I/O, and initialization
public partial class ModuleEditorViewModel
{
    private async Task InitializeGameDataServiceAsync()
    {
        try
        {
            _gameDataService = await Task.Run(() => new GameDataService());
            if (_gameDataService.IsConfigured)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "ModuleEditor: GameDataService initialized");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "ModuleEditor: GameDataService not configured (no game path)");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"ModuleEditor: Failed to initialize GameDataService: {ex.Message}");
        }

        RefreshAvailableTlkLanguages();
    }

    public void SetParentWindow(Window window)
    {
        _parentWindow = window;
    }

    public async Task LoadCurrentModuleAsync()
    {
        var currentPath = RadoubSettings.Instance.CurrentModulePath;
        if (string.IsNullOrEmpty(currentPath))
        {
            StatusText = "No module selected. Open a module in Trebuchet first.";
            return;
        }

        await LoadModuleAsync(currentPath);
    }

    public async Task LoadModuleAsync(string path)
    {
        IsLoading = true;
        StatusText = "Loading module...";

        try
        {
            _modulePath = path;
            _modFilePath = null;
            _workingDirectoryPath = null;
            _isFromModFile = false;
            _isReadOnly = false;

            // Check if path is a .mod file or a directory
            if (File.Exists(path) && path.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
            {
                // Check for unpacked working directory
                var moduleName = Path.GetFileNameWithoutExtension(path);
                var moduleDir = Path.GetDirectoryName(path);
                var workingDir = FindWorkingDirectory(moduleDir, moduleName);

                if (workingDir != null)
                {
                    // Unpacked directory exists - load from there (editable)
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Found unpacked module directory: {UnifiedLogger.SanitizePath(workingDir)}");
                    _workingDirectoryPath = workingDir;
                    _modFilePath = path;
                    _modulePath = workingDir;  // Save to working directory, not .mod file
                    await LoadFromDirectoryAsync(workingDir);
                    _isReadOnly = false;
                }
                else
                {
                    // No unpacked directory - auto-unpack the .mod file (#1384)
                    var targetDir = Path.Combine(moduleDir!, moduleName);
                    StatusText = "Unpacking module...";

                    var resourceCount = await Task.Run(() => UnpackModuleToDirectory(path, targetDir));
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Auto-unpacked {resourceCount} resources to {UnifiedLogger.SanitizePath(targetDir)}");

                    _workingDirectoryPath = targetDir;
                    _modFilePath = path;
                    _modulePath = targetDir;
                    await LoadFromDirectoryAsync(targetDir);
                    _isReadOnly = false;
                }
            }
            else if (Directory.Exists(path))
            {
                await LoadFromDirectoryAsync(path);
                _isReadOnly = false;
            }
            else
            {
                StatusText = "Invalid module path";
                return;
            }

            PopulateViewModelFromIfo();
            SyncToRadoubSettings();
            SyncDefaultBicToSettings();
            IsModuleLoaded = true;
            IsModuleReadOnly = _isReadOnly;
            IsVersionUnlocked = false;  // Always start with version locked
            HasUnsavedChanges = false;

            var statusSuffix = _isReadOnly ? " (Read-only - module not unpacked)" : "";
            StatusText = $"Loaded: {Path.GetFileName(_modulePath)}{statusSuffix}";
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load module: {ex.Message}");
            StatusText = $"Error: {ex.Message}";
            IsModuleLoaded = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Find the unpacked working directory for a module.
    /// NWN toolset uses temp0, temp01, or the module name as working directory.
    /// </summary>
    private static string? FindWorkingDirectory(string? moduleDir, string moduleName)
    {
        if (string.IsNullOrEmpty(moduleDir))
            return null;

        // Check in priority order:
        // 1. Module name folder (e.g., "MyModule/")
        // 2. temp0 folder (NWN toolset default)
        // 3. temp1 folder (alternate toolset folder)
        var candidates = new[]
        {
            Path.Combine(moduleDir, moduleName),
            Path.Combine(moduleDir, "temp0"),
            Path.Combine(moduleDir, "temp1")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, "module.ifo")))
            {
                return candidate;
            }
        }

        return null;
    }

    private async Task LoadFromModFileAsync(string modPath)
    {
        await Task.Run(() =>
        {
            var erf = ErfReader.ReadMetadataOnly(modPath);
            var ifoEntry = erf.FindResource("module", ResourceTypes.Ifo);

            if (ifoEntry == null)
            {
                throw new InvalidDataException("module.ifo not found in MOD file");
            }

            var ifoData = ErfReader.ExtractResource(modPath, ifoEntry);
            _ifoFile = IfoReader.Read(ifoData);
            _modFilePath = modPath;
            _isFromModFile = true;
        });
    }

    private async Task LoadFromDirectoryAsync(string dirPath)
    {
        await Task.Run(() =>
        {
            var ifoPath = Path.Combine(dirPath, "module.ifo");
            if (!File.Exists(ifoPath))
            {
                throw new FileNotFoundException("module.ifo not found in directory");
            }

            _ifoFile = IfoReader.Read(ifoPath);
            _isFromModFile = false;
        });
    }
}
