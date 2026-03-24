using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ItemEditor.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Uti;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using System.IO;
using System.Threading.Tasks;

namespace ItemEditor.Views;

public partial class MainWindow
{
    // --- File Operations ---

    private async Task<bool> OpenFileAsync(string filePath)
    {
        try
        {
            _isLoading = true;
            UpdateStatus($"Opening {Path.GetFileName(filePath)}...");

            // Release lock on previous file if any
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                FileSessionLockService.ReleaseLock(_currentFilePath);
                _documentState.IsReadOnly = false;
            }

            // Check for file lock from another tool instance
            var lockResult = FileSessionLockService.AcquireLock(filePath, "Relique");
            if (lockResult == LockResult.LockedByOther)
            {
                var lockInfo = FileSessionLockService.CheckLock(filePath);
                var toolName = lockInfo?.ToolName ?? "another tool";
                UnifiedLogger.LogApplication(LogLevel.WARN, $"File locked by {toolName} — opening read-only: {UnifiedLogger.SanitizePath(filePath)}");
                UpdateStatus($"File is open in {toolName} — opening read-only");
                _documentState.IsReadOnly = true;
            }

            var item = UtiReader.Read(filePath);
            _currentItem = item;
            _currentFilePath = filePath;
            _documentState.ClearDirty();
            // Always update title — ClearDirty only fires when transitioning from dirty
            Title = _documentState.GetTitle();

            // Sync ResRef from filename (Aurora Engine requires they match)
            var fileResRef = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            if (_currentItem.TemplateResRef != fileResRef)
            {
                _currentItem.TemplateResRef = fileResRef;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Synced ResRef to filename: {fileResRef}");
            }

            PopulateEditor();
            OnPropertyChanged(nameof(HasFile));
            AddRecentFile(filePath);

            // Update item browser panel
            ItemBrowserPanel.CurrentFilePath = filePath;
            var moduleDir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(moduleDir))
            {
                ItemBrowserPanel.ModulePath = moduleDir;
            }

            // Update search bar file path
            this.FindControl<SearchBar>("FileSearchBar")?.UpdateFilePath(filePath);

            UpdateStatus("Ready");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Opened: {UnifiedLogger.SanitizePath(filePath)}");
            return true;
        }
        catch (System.Exception ex)
        {
            UpdateStatus("Error opening file");
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open {UnifiedLogger.SanitizePath(filePath)}: {ex.Message}");
            await ShowErrorAsync($"Failed to open file:\n{ex.Message}");
            return false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task<bool> SaveCurrentFileAsync()
    {
        if (_currentItem == null || string.IsNullOrEmpty(_currentFilePath))
            return false;

        if (_documentState.IsReadOnly)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Save blocked: file is read-only (locked by another tool): {UnifiedLogger.SanitizePath(_currentFilePath)}");
            UpdateStatus("Cannot save: file is open read-only (locked by another tool).");
            return false;
        }

        try
        {
            // Validate variables before save
            var varError = ValidateVariables();
            if (varError != null && varError.Contains("Duplicate"))
            {
                await ShowErrorAsync(varError);
                return false;
            }
            if (varError != null)
            {
                // Warn about empty names but allow save
                UnifiedLogger.LogApplication(LogLevel.WARN, varError);
            }

            UpdateStatus("Saving...");

            // Sync ResRef to match filename (Aurora Engine requires they match)
            var saveResRef = Path.GetFileNameWithoutExtension(_currentFilePath).ToLowerInvariant();
            _currentItem.TemplateResRef = saveResRef;
            if (_itemViewModel != null)
            {
                _isLoading = true;
                _itemViewModel.ResRef = saveResRef;
                _isLoading = false;
            }

            UpdateVarTable();
            UtiWriter.Write(_currentItem, _currentFilePath);
            _documentState.ClearDirty();

            UpdateStatus("Ready");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Saved: {UnifiedLogger.SanitizePath(_currentFilePath)}");
            return true;
        }
        catch (System.Exception ex)
        {
            UpdateStatus("Error saving file");
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save {UnifiedLogger.SanitizePath(_currentFilePath)}: {ex.Message}");
            await ShowErrorAsync($"Failed to save file:\n{ex.Message}");
            return false;
        }
    }

    private async Task<bool> SaveAsAsync()
    {
        if (_currentItem == null)
            return false;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Item As",
            DefaultExtension = "uti",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Item Blueprint") { Patterns = new[] { "*.uti" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            },
            SuggestedFileName = Path.GetFileName(_currentFilePath ?? "item.uti")
        });

        if (file == null)
            return false;

        var path = file.Path.LocalPath;
        _currentFilePath = path;

        var result = await SaveCurrentFileAsync();
        if (result)
        {
            AddRecentFile(path);
        }
        return result;
    }

    // --- Recent Files ---

    private void PopulateRecentFiles()
    {
        RecentFilesMenu.Items.Clear();
        var recentFiles = SettingsService.Instance.RecentFiles;

        if (recentFiles.Count == 0)
        {
            var emptyItem = new MenuItem { Header = "(none)", IsEnabled = false };
            RecentFilesMenu.Items.Add(emptyItem);
            return;
        }

        foreach (var path in recentFiles)
        {
            var menuItem = new MenuItem
            {
                Header = Path.GetFileName(path),
                Tag = path
            };
            ToolTip.SetTip(menuItem, UnifiedLogger.SanitizePath(path));
            menuItem.Click += async (_, _) =>
            {
                if (_isDirty)
                {
                    var result = await PromptSaveChangesAsync();
                    if (result == SavePromptResult.Cancel) return;
                    if (result == SavePromptResult.Save && !await SaveCurrentFileAsync()) return;
                }
                await OpenFileAsync((string)menuItem.Tag!);
            };
            RecentFilesMenu.Items.Add(menuItem);
        }
    }

    private void AddRecentFile(string filePath)
    {
        SettingsService.Instance.AddRecentFile(filePath);
        PopulateRecentFiles();
    }
}
