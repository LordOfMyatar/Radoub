using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Radoub.Dictionary;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using System;
using System.Diagnostics;
using System.IO;

namespace Manifest.Views;

/// <summary>
/// SettingsWindow partial: Spell check and dictionary management settings.
/// </summary>
public partial class SettingsWindow
{
    #region Spell Check

    private void LoadSpellCheckSettings()
    {
        SpellCheckEnabledCheckBox.IsChecked = DictionarySettingsService.Instance.SpellCheckEnabled;
        UpdateDictionaryInfo();
    }

    private void OnSpellCheckEnabledChanged(object? sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        DictionarySettingsService.Instance.SpellCheckEnabled = SpellCheckEnabledCheckBox.IsChecked ?? true;
    }

    private void UpdateDictionaryInfo()
    {
        var wordCount = SpellCheckService.Instance.GetCustomWordCount();
        DictionaryInfoText.Text = $"Custom dictionary: {wordCount} words";
    }

    #endregion

    #region Dictionaries

    private DictionaryDiscovery? _discovery;

    private void LoadDictionarySettings()
    {
        _discovery = new DictionaryDiscovery(DictionaryDiscovery.GetDefaultUserDictionaryPath());
        LoadPrimaryLanguageList();
        LoadCustomDictionaryList();
    }

    private void LoadPrimaryLanguageList()
    {
        if (_discovery == null) return;

        PrimaryLanguageComboBox.Items.Clear();
        var languages = _discovery.GetAvailableLanguages();
        var currentLanguage = DictionarySettingsService.Instance.PrimaryLanguage;

        foreach (var lang in languages)
        {
            var item = new LanguageListItem
            {
                Id = lang.Id,
                DisplayName = lang.Name,
                IsBundled = lang.IsBundled
            };

            PrimaryLanguageComboBox.Items.Add(item);

            if (lang.Id == currentLanguage)
            {
                PrimaryLanguageComboBox.SelectedItem = item;
            }
        }

        // Default to first item if no selection
        if (PrimaryLanguageComboBox.SelectedItem == null && PrimaryLanguageComboBox.Items.Count > 0)
        {
            PrimaryLanguageComboBox.SelectedIndex = 0;
        }
    }

    private void LoadCustomDictionaryList()
    {
        if (_discovery == null) return;

        CustomDictionariesListBox.Items.Clear();
        var customDicts = _discovery.GetAvailableCustomDictionaries();

        foreach (var dict in customDicts)
        {
            var isEnabled = DictionarySettingsService.Instance.IsCustomDictionaryEnabled(dict.Id);

            var checkBox = new CheckBox
            {
                Content = dict.Name,
                IsChecked = isEnabled,
                Tag = dict.Id
            };

            ToolTip.SetTip(checkBox, dict.Description ?? $"Custom dictionary: {dict.Id}");
            checkBox.IsCheckedChanged += OnCustomDictionaryToggled;

            CustomDictionariesListBox.Items.Add(checkBox);
        }

        // Show message if no custom dictionaries found
        if (customDicts.Count == 0)
        {
            CustomDictionariesListBox.Items.Add(new TextBlock
            {
                Text = "No custom dictionaries found",
                FontStyle = FontStyle.Italic,
                Foreground = Brushes.Gray,
                Margin = new Thickness(5)
            });
        }
    }

    private void OnPrimaryLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (PrimaryLanguageComboBox.SelectedItem is LanguageListItem item)
        {
            DictionarySettingsService.Instance.PrimaryLanguage = item.Id;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Primary dictionary language changed to: {item.DisplayName}");
        }
    }

    private void OnCustomDictionaryToggled(object? sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        if (sender is CheckBox checkBox && checkBox.Tag is string dictionaryId)
        {
            var isEnabled = checkBox.IsChecked ?? false;
            DictionarySettingsService.Instance.SetCustomDictionaryEnabled(dictionaryId, isEnabled);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Custom dictionary '{dictionaryId}' {(isEnabled ? "enabled" : "disabled")}");
        }
    }

    private void OnOpenDictionariesFolderClick(object? sender, RoutedEventArgs e)
    {
        var path = DictionaryDiscovery.GetDefaultUserDictionaryPath();

        // Ensure directory exists
        Directory.CreateDirectory(path);

        try
        {
            // Open in file explorer
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Opened dictionaries folder: {SanitizePath(path)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open dictionaries folder: {ex.Message}");
        }
    }

    private void OnRefreshDictionariesClick(object? sender, RoutedEventArgs e)
    {
        _discovery?.ClearCache();
        LoadPrimaryLanguageList();
        LoadCustomDictionaryList();
        UnifiedLogger.LogApplication(LogLevel.INFO, "Dictionary list refreshed");
    }

    /// <summary>
    /// Helper class for language combo box items.
    /// </summary>
    private class LanguageListItem
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsBundled { get; set; }

        public override string ToString()
        {
            return IsBundled ? DisplayName : $"{DisplayName} (installed)";
        }
    }

    #endregion
}
