using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Bic;
using Radoub.Formats.Common;
using Radoub.Formats.Utc;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Quartermaster.Views;

/// <summary>
/// MainWindow partial class for file operations (Open/Save/Load/Recent files).
/// Extracted from MainWindow.axaml.cs for maintainability (#582).
/// </summary>
public partial class MainWindow
{
    #region Recent Files Menu

    private void UpdateRecentFilesMenu()
    {
        RecentFilesMenu.Items.Clear();

        var recentFiles = SettingsService.Instance.RecentFiles;

        if (recentFiles.Count == 0)
        {
            var emptyItem = new MenuItem { Header = "(No recent files)", IsEnabled = false };
            RecentFilesMenu.Items.Add(emptyItem);
            return;
        }

        foreach (var filePath in recentFiles)
        {
            var fileName = Path.GetFileName(filePath);
            var displayPath = UnifiedLogger.SanitizePath(filePath);

            var menuItem = new MenuItem
            {
                Header = fileName,
                Tag = filePath
            };
            ToolTip.SetTip(menuItem, displayPath);
            menuItem.Click += OnRecentFileClick;

            RecentFilesMenu.Items.Add(menuItem);
        }

        RecentFilesMenu.Items.Add(new Separator());

        var clearItem = new MenuItem { Header = "Clear Recent Files" };
        clearItem.Click += OnClearRecentFilesClick;
        RecentFilesMenu.Items.Add(clearItem);
    }

    private async void OnRecentFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string filePath)
        {
            // Close the entire menu hierarchy before async work
            // Find the top-level File menu and close it
            if (RecentFilesMenu.Parent is MenuItem fileMenu)
            {
                fileMenu.Close();
            }

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
        }
    }

    private void OnClearRecentFilesClick(object? sender, RoutedEventArgs e)
    {
        SettingsService.Instance.ClearRecentFiles();
        UpdateRecentFilesMenu();
    }

    #endregion

    #region Menu Click Handlers

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        await OpenFile();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        await SaveFile();
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        await SaveFileAs();
    }

    private void OnCloseFileClick(object? sender, RoutedEventArgs e)
    {
        CloseFile();
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region File Operations

    private async Task OpenFile()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Creature File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Creature Files") { Patterns = new[] { "*.utc", "*.bic" } },
                new FilePickerFileType("Creature Blueprints") { Patterns = new[] { "*.utc" } },
                new FilePickerFileType("Player Characters") { Patterns = new[] { "*.bic" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            var file = files[0];
            await LoadFile(file.Path.LocalPath);
        }
    }

    private async Task LoadFile(string filePath)
    {
        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            _isBicFile = extension == ".bic";

            if (_isBicFile)
            {
                _currentCreature = BicReader.Read(filePath);
                UnifiedLogger.LogCreature(LogLevel.INFO, $"Loaded BIC (player character): {UnifiedLogger.SanitizePath(filePath)}");
            }
            else
            {
                _currentCreature = UtcReader.Read(filePath);
                UnifiedLogger.LogCreature(LogLevel.INFO, $"Loaded UTC (creature blueprint): {UnifiedLogger.SanitizePath(filePath)}");
            }

            _currentFilePath = filePath;
            _isDirty = false;

            PopulateInventoryUI();
            UpdateCharacterHeader();
            LoadAllPanels(_currentCreature);
            UpdateTitle();
            UpdateStatus($"Loaded: {Path.GetFileName(filePath)}");
            UpdateInventoryCounts();
            OnPropertyChanged(nameof(HasFile));

            SettingsService.Instance.AddRecentFile(filePath);
            UpdateRecentFilesMenu();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogCreature(LogLevel.ERROR, $"Failed to load creature: {ex.Message}");
            UpdateStatus($"Error loading file: {ex.Message}");
            await ShowErrorDialog("Load Error", $"Failed to load creature file:\n{ex.Message}");
        }
    }

    private async Task SaveFile()
    {
        if (_currentCreature == null || string.IsNullOrEmpty(_currentFilePath)) return;

        try
        {
            if (_isBicFile && _currentCreature is BicFile bicFile)
            {
                BicWriter.Write(bicFile, _currentFilePath);
            }
            else
            {
                UtcWriter.Write(_currentCreature, _currentFilePath);
            }

            _isDirty = false;
            UpdateTitle();
            UpdateStatus($"Saved: {Path.GetFileName(_currentFilePath)}");

            UnifiedLogger.LogCreature(LogLevel.INFO, $"Saved creature: {UnifiedLogger.SanitizePath(_currentFilePath)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogCreature(LogLevel.ERROR, $"Failed to save creature: {ex.Message}");
            UpdateStatus($"Error saving file: {ex.Message}");
            await ShowErrorDialog("Save Error", $"Failed to save creature file:\n{ex.Message}");
        }
    }

    private async Task SaveFileAs()
    {
        if (_currentCreature == null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Creature As",
            DefaultExtension = _isBicFile ? ".bic" : ".utc",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Creature Blueprint") { Patterns = new[] { "*.utc" } },
                new FilePickerFileType("Player Character") { Patterns = new[] { "*.bic" } }
            }
        });

        if (file != null)
        {
            _currentFilePath = file.Path.LocalPath;
            _isBicFile = Path.GetExtension(_currentFilePath).ToLowerInvariant() == ".bic";
            await SaveFile();
            UpdateTitle();
            SettingsService.Instance.AddRecentFile(_currentFilePath);
            UpdateRecentFilesMenu();
        }
    }

    private void CloseFile()
    {
        _currentCreature = null;
        _currentFilePath = null;
        _isDirty = false;
        _isBicFile = false;

        ClearInventoryUI();
        UpdateCharacterHeader();
        ClearAllPanels();
        UpdateTitle();
        UpdateStatus("Ready");
        UpdateInventoryCounts();
        OnPropertyChanged(nameof(HasFile));
    }

    #endregion
}
