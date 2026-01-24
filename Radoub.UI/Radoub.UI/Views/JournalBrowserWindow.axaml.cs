using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace Radoub.UI.Views;

/// <summary>
/// Simple journal entry for the browser.
/// </summary>
public class JournalBrowserEntry
{
    public string Name { get; set; } = "";
    public string? FilePath { get; set; }
}

/// <summary>
/// Browser window for selecting journal files (.jrl) from a module directory.
/// Journals are typically just module.jrl in an unpacked module folder.
/// </summary>
public partial class JournalBrowserWindow : Window
{
    private readonly IScriptBrowserContext? _context;
    private List<JournalBrowserEntry> _journals = new();
    private JournalBrowserEntry? _selectedEntry;
    private string? _overridePath;
    private bool _confirmed;

    /// <summary>
    /// Gets the full selected entry with file path info. Only valid if confirmed.
    /// </summary>
    public JournalBrowserEntry? SelectedEntry => _confirmed ? _selectedEntry : null;

    public JournalBrowserWindow() : this(null)
    {
    }

    public JournalBrowserWindow(IScriptBrowserContext? context)
    {
        InitializeComponent();
        _context = context;

        UpdateLocationDisplay();
        LoadJournals();
    }

    private void UpdateLocationDisplay()
    {
        if (!string.IsNullOrEmpty(_overridePath))
        {
            LocationPathLabel.Text = UnifiedLogger.SanitizePath(_overridePath);
            LocationPathLabel.Foreground = new SolidColorBrush(Colors.White);
            ResetLocationButton.IsVisible = true;
        }
        else
        {
            var currentDir = _context?.CurrentFileDirectory;
            if (!string.IsNullOrEmpty(currentDir))
            {
                LocationPathLabel.Text = UnifiedLogger.SanitizePath(currentDir);
                LocationPathLabel.Foreground = new SolidColorBrush(Colors.LightGray);
            }
            else
            {
                LocationPathLabel.Text = "(no module loaded - use browse...)";
                LocationPathLabel.Foreground = new SolidColorBrush(Colors.Orange);
            }
            ResetLocationButton.IsVisible = false;
        }
    }

    private string? GetCurrentDirectory()
    {
        return _overridePath ?? _context?.CurrentFileDirectory;
    }

    private async void OnBrowseLocationClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            IStorageFolder? suggestedStart = null;
            var currentDir = GetCurrentDirectory();
            if (!string.IsNullOrEmpty(currentDir))
            {
                var parentDir = Path.GetDirectoryName(currentDir);
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    suggestedStart = await StorageProvider.TryGetFolderFromPathAsync(parentDir);
                }
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Module Folder",
                AllowMultiple = false,
                SuggestedStartLocation = suggestedStart
            });

            if (folders.Count > 0)
            {
                var folder = folders[0];
                _overridePath = folder.Path.LocalPath;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Journal browser: Override path set to {UnifiedLogger.SanitizePath(_overridePath)}");

                UpdateLocationDisplay();
                LoadJournals();
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error selecting folder: {ex.Message}");
        }
    }

    private void OnResetLocationClick(object? sender, RoutedEventArgs e)
    {
        _overridePath = null;
        UnifiedLogger.LogApplication(LogLevel.INFO, "Journal browser: Reset to auto-detected path");
        UpdateLocationDisplay();
        LoadJournals();
    }

    private void LoadJournals()
    {
        _journals.Clear();
        JournalListBox.Items.Clear();

        var currentDir = GetCurrentDirectory();
        if (string.IsNullOrEmpty(currentDir) || !Directory.Exists(currentDir))
        {
            JournalCountLabel.Text = "No module folder selected";
            JournalCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
            return;
        }

        try
        {
            var journalFiles = Directory.GetFiles(currentDir, "*.jrl", SearchOption.TopDirectoryOnly);

            foreach (var journalFile in journalFiles)
            {
                var journalName = Path.GetFileNameWithoutExtension(journalFile);
                _journals.Add(new JournalBrowserEntry
                {
                    Name = journalName,
                    FilePath = journalFile
                });
            }

            _journals = _journals.OrderBy(j => j.Name).ToList();

            foreach (var journal in _journals)
            {
                JournalListBox.Items.Add(journal);
            }

            if (_journals.Count == 0)
            {
                JournalCountLabel.Text = "No .jrl files found in module folder";
                JournalCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
            }
            else
            {
                JournalCountLabel.Text = $"{_journals.Count} journal{(_journals.Count == 1 ? "" : "s")}";
                JournalCountLabel.Foreground = new SolidColorBrush(Colors.White);

                // Auto-select if only one journal (common case: module.jrl)
                if (_journals.Count == 1)
                {
                    JournalListBox.SelectedIndex = 0;
                }
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Journal Browser: Found {_journals.Count} journals in {UnifiedLogger.SanitizePath(currentDir)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning for journals: {ex.Message}");
            JournalCountLabel.Text = $"Error: {ex.Message}";
            JournalCountLabel.Foreground = new SolidColorBrush(Colors.Red);
        }
    }

    private void OnJournalSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (JournalListBox.SelectedItem is JournalBrowserEntry entry)
        {
            _selectedEntry = entry;
            SelectedJournalLabel.Text = entry.Name;
        }
        else
        {
            _selectedEntry = null;
            SelectedJournalLabel.Text = "(none)";
        }
    }

    private void OnJournalDoubleClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedEntry != null)
        {
            _confirmed = true;
            Close(_selectedEntry.Name);
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close(_selectedEntry?.Name);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    #region Title Bar Events

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    #endregion
}
