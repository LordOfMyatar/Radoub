using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Manifest.Services;
using Radoub.Formats.Jrl;
using Radoub.Formats.Logging;
using Radoub.UI.Controls;
using Radoub.UI.Views;
using DirtyCheckResult = Radoub.UI.Services.DirtyCheckResult;
using FileOperationsHelper = Radoub.UI.Services.FileOperationsHelper;
using FileSessionLockService = Radoub.UI.Services.FileSessionLockService;
using LockResult = Radoub.UI.Services.LockResult;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Manifest.Views;

/// <summary>
/// MainWindow partial: File operations (open, save, new, recent files, auto-load).
/// </summary>
public partial class MainWindow
{
    private void UpdateRecentFilesMenu()
    {
        Radoub.UI.Services.RecentFilesMenuHelper.Populate(
            RecentFilesMenu,
            SettingsService.Instance.RecentFiles,
            async filePath =>
            {
                if (File.Exists(filePath))
                {
                    await LoadFile(filePath);
                }
                else
                {
                    UpdateStatus($"File not found: {Path.GetFileName(filePath)}");
                    SettingsService.Instance.RemoveRecentFile(filePath);
                    UpdateRecentFilesMenu();
                }
            },
            () =>
            {
                SettingsService.Instance.ClearRecentFiles();
                UpdateRecentFilesMenu();
            });
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        await OpenFile();
    }

    private async void OnNewJournalClick(object? sender, RoutedEventArgs e)
    {
        await CreateNewJournal();
    }

    private async void OnOpenFromModuleClick(object? sender, RoutedEventArgs e)
    {
        await OpenFromModule();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        await SaveFile();
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task OpenFile()
    {
        // Use custom JournalBrowserWindow for consistent UX (#1085)
        var context = new ManifestBrowserContext(_currentFilePath);
        var browser = new JournalBrowserWindow(context);
        await browser.ShowDialog(this);

        // Check if user selected a journal
        var selectedEntry = browser.SelectedEntry;
        if (selectedEntry?.FilePath != null)
        {
            await LoadFile(selectedEntry.FilePath);
        }
    }

    private async Task OpenFromModule()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Module Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var folder = folders[0];
            var modulePath = folder.Path.LocalPath;
            var jrlPath = Path.Combine(modulePath, "module.jrl");

            if (File.Exists(jrlPath))
            {
                await LoadFile(jrlPath);
                UnifiedLogger.LogJournal(LogLevel.INFO, $"Opened module.jrl from: {UnifiedLogger.SanitizePath(modulePath)}");
            }
            else
            {
                UpdateStatus("No module.jrl found in selected folder");
                ShowErrorDialog("File Not Found", $"No module.jrl file found in:\n{UnifiedLogger.SanitizePath(modulePath)}");
            }
        }
    }

    private async Task LoadFile(string filePath)
    {
        await Task.CompletedTask; // Async signature preserved for future async I/O
        try
        {
            // Release lock on previous file if any
            if (!string.IsNullOrEmpty(_documentState.CurrentFilePath))
            {
                FileSessionLockService.ReleaseLock(_documentState.CurrentFilePath);
                _documentState.IsReadOnly = false;
            }

            // Check for file lock from another tool instance
            var lockResult = FileSessionLockService.AcquireLock(filePath, "Manifest");
            if (lockResult == LockResult.LockedByOther)
            {
                var lockInfo = FileSessionLockService.CheckLock(filePath);
                var toolName = lockInfo?.ToolName ?? "another tool";
                UnifiedLogger.LogApplication(LogLevel.WARN, $"File locked by {toolName} — opening read-only: {UnifiedLogger.SanitizePath(filePath)}");
                UpdateStatus($"File is open in {toolName} — opening read-only");
                _documentState.IsReadOnly = true;
            }

            _currentJrl = JrlReader.Read(filePath);
            _documentState.CurrentFilePath = filePath;
            _documentState.ClearDirty();

            // Clear selection and update UI
            _selectedItem = null;
            UpdateTree();
            UpdatePropertyPanel();
            UpdateTitle();
            UpdateStatus($"Loaded: {Path.GetFileName(filePath)}");
            UpdateTlkStatus();
            UpdateStatusBarCounts();
            OnPropertyChanged(nameof(HasFile));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanAddEntry));

            // Add to recent files
            SettingsService.Instance.AddRecentFile(filePath);
            UpdateRecentFilesMenu();

            // Update search bar file path
            this.FindControl<SearchBar>("FileSearchBar")?.UpdateFilePath(filePath);

            UnifiedLogger.LogJournal(LogLevel.INFO, $"Loaded journal: {UnifiedLogger.SanitizePath(filePath)} ({_currentJrl.Categories.Count} categories)");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogJournal(LogLevel.ERROR, $"Failed to load journal: {ex.Message}");
            UpdateStatus($"Error loading file: {ex.Message}");
            ShowErrorDialog("Load Error", $"Failed to load journal file:\n{ex.Message}");
        }
    }

    private void UpdateTlkStatus()
    {
        TlkStatusText.Text = TlkService.Instance.GetTlkStatusSummary();
    }

    private void UpdateStatusBarCounts()
    {
        if (_currentJrl == null)
        {
            CountsText.Text = "";
            FilePathText.Text = "";
            return;
        }

        var categoryCount = _currentJrl.Categories.Count;
        var entryCount = _currentJrl.Categories.Sum(c => c.Entries.Count);
        CountsText.Text = $"{categoryCount} categories, {entryCount} entries";
        FilePathText.Text = _currentFilePath != null ? UnifiedLogger.SanitizePath(_currentFilePath) : "";
    }

    private async Task SaveFile()
    {
        await Task.CompletedTask; // Async signature preserved for future async I/O
        if (_currentJrl == null || string.IsNullOrEmpty(_currentFilePath)) return;

        if (_documentState.IsReadOnly)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Save blocked: file is read-only (locked by another tool): {UnifiedLogger.SanitizePath(_currentFilePath)}");
            UpdateStatus("Cannot save: file is open read-only (locked by another tool).");
            return;
        }

        try
        {
            JrlWriter.Write(_currentJrl, _currentFilePath);
            _documentState.ClearDirty();
            UpdateTitle();
            UpdateStatus($"Saved: {Path.GetFileName(_currentFilePath)}");

            UnifiedLogger.LogJournal(LogLevel.INFO, $"Saved journal: {UnifiedLogger.SanitizePath(_currentFilePath)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogJournal(LogLevel.ERROR, $"Failed to save journal: {ex.Message}");
            UpdateStatus($"Error saving file: {ex.Message}");
            ShowErrorDialog("Save Error", $"Failed to save journal file:\n{ex.Message}");
        }
    }

    private async Task CreateNewJournal()
    {
        // Check for unsaved changes
        var dirtyResult = await FileOperationsHelper.CheckDirtyAsync(this, _documentState);
        if (dirtyResult == DirtyCheckResult.Cancel) return;
        if (dirtyResult == DirtyCheckResult.Save) await SaveFile();

        // Release lock on previous file
        if (!string.IsNullOrEmpty(_currentFilePath))
            FileSessionLockService.ReleaseLock(_currentFilePath);
        _documentState.IsReadOnly = false;

        // Prompt for save location with default filename
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Create New Journal",
            DefaultExtension = "jrl",
            SuggestedFileName = "module.jrl",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Journal Files") { Patterns = new[] { "*.jrl" } }
            }
        });

        if (file == null)
        {
            return; // User cancelled
        }

        var filePath = file.Path.LocalPath;

        try
        {
            // Create empty JRL structure
            var newJrl = new JrlFile
            {
                FileType = "JRL ",
                FileVersion = "V3.2",
                Categories = new List<JournalCategory>()
            };

            // Save the empty file
            JrlWriter.Write(newJrl, filePath);

            // Load the newly created file
            _currentJrl = newJrl;
            _currentFilePath = filePath;
            _documentState.ClearDirty();

            UpdateTree();
            UpdateTitle();
            UpdateStatusBarCounts();
            UpdateStatus($"Created: {Path.GetFileName(filePath)}");

            // Add to recent files
            SettingsService.Instance.AddRecentFile(filePath);
            UpdateRecentFilesMenu();

            OnPropertyChanged(nameof(HasFile));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanAddEntry));

            UnifiedLogger.LogJournal(LogLevel.INFO, $"Created new journal: {UnifiedLogger.SanitizePath(filePath)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogJournal(LogLevel.ERROR, $"Failed to create journal: {ex.Message}");
            UpdateStatus($"Error creating file: {ex.Message}");
            ShowErrorDialog("Create Error", $"Failed to create journal file:\n{ex.Message}");
        }
    }

    /// <summary>
    /// Handle command line arguments for file loading and navigation.
    /// Enables cross-tool integration (e.g., Parley's "Open in Manifest" feature).
    /// If no file is specified, auto-detect journal from Trebuchet's current module.
    /// </summary>
    private async Task HandleStartupFileAsync()
    {
        var options = CommandLineService.Options;

        if (!string.IsNullOrEmpty(options.FilePath))
        {
            if (!File.Exists(options.FilePath))
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Command line file not found: {UnifiedLogger.SanitizePath(options.FilePath)}");
                UpdateStatus($"File not found: {Path.GetFileName(options.FilePath)}");
                return;
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Loading file from command line: {UnifiedLogger.SanitizePath(options.FilePath)}");
            await LoadFile(options.FilePath);

            if (!string.IsNullOrEmpty(options.QuestTag))
            {
                NavigateToQuest(options.QuestTag, options.EntryId);
            }
            return;
        }

        // No --file specified: try to auto-detect journal from Trebuchet's current module
        await TryAutoLoadModuleJournalAsync();
    }

    /// <summary>
    /// Auto-detect and load the journal file from Trebuchet's currently selected module.
    /// Modules typically have exactly one .jrl file.
    /// </summary>
    private async Task TryAutoLoadModuleJournalAsync()
    {
        var context = new ManifestBrowserContext(null);
        var moduleDir = context.CurrentFileDirectory;

        if (string.IsNullOrEmpty(moduleDir) || !Directory.Exists(moduleDir))
            return;

        try
        {
            var jrlFiles = Directory.GetFiles(moduleDir, "*.jrl", SearchOption.TopDirectoryOnly);

            if (jrlFiles.Length == 1)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Auto-loading journal from module: {UnifiedLogger.SanitizePath(jrlFiles[0])}");
                UpdateStatus("Loading module journal...");
                await LoadFile(jrlFiles[0]);
            }
            else if (jrlFiles.Length > 1)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Multiple .jrl files in module directory ({jrlFiles.Length}), skipping auto-load");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Auto-load journal failed: {ex.Message}");
        }
    }
}
