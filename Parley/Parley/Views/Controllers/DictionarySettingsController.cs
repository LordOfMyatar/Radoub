using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using DialogEditor.Services;
using Radoub.Dictionary;
using Radoub.Dictionary.Models;
using Radoub.Formats.Logging;

namespace DialogEditor.Views.Controllers
{
    /// <summary>
    /// Controller for Dictionary settings section in SettingsWindow.
    /// Handles: Primary language, custom dictionaries, dictionary folder operations.
    /// </summary>
    public class DictionarySettingsController
    {
        private readonly Window _window;
        private readonly Func<bool> _isInitializing;
        private DictionaryDiscovery? _dictionaryDiscovery;

        public DictionarySettingsController(Window window, Func<bool> isInitializing)
        {
            _window = window;
            _isInitializing = isInitializing;
        }

        public void LoadSettings()
        {
            _dictionaryDiscovery = new DictionaryDiscovery(DictionaryDiscovery.GetDefaultUserDictionaryPath());

            LoadPrimaryLanguageList();
            LoadCustomDictionaryList();
            LoadDictionaryPathInfo();
        }

        private void LoadPrimaryLanguageList()
        {
            var languageComboBox = _window.FindControl<ComboBox>("PrimaryLanguageComboBox");
            if (languageComboBox == null || _dictionaryDiscovery == null)
                return;

            var languages = _dictionaryDiscovery.GetAvailableLanguages();
            var currentLanguage = DictionarySettingsService.Instance.PrimaryLanguage;

            // Create display items
            var items = languages.Select(lang => new LanguageListItem
            {
                Id = lang.Id,
                DisplayName = lang.IsBundled ? $"{lang.Name} (bundled)" : lang.Name,
                Info = lang
            }).ToList();

            languageComboBox.ItemsSource = items;
            languageComboBox.DisplayMemberBinding = new Binding("DisplayName");

            // Select current language
            var currentItem = items.FirstOrDefault(i => i.Id == currentLanguage);
            if (currentItem != null)
            {
                languageComboBox.SelectedItem = currentItem;
            }
            else if (items.Count > 0)
            {
                languageComboBox.SelectedItem = items[0];
            }
        }

        private void LoadCustomDictionaryList()
        {
            var dictionariesListBox = _window.FindControl<ListBox>("CustomDictionariesListBox");
            if (dictionariesListBox == null || _dictionaryDiscovery == null)
                return;

            var customDictionaries = _dictionaryDiscovery.GetAvailableCustomDictionaries();
            var dictSettings = DictionarySettingsService.Instance;
            var dictionaryItems = new List<Control>();

            foreach (var dict in customDictionaries)
            {
                var isEnabled = dictSettings.IsCustomDictionaryEnabled(dict.Id);

                var panel = new StackPanel
                {
                    Spacing = 5,
                    Margin = new Thickness(5)
                };

                var headerPanel = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto")
                };

                var badge = dict.IsBundled ? " [BUNDLED]" : "";
                var wordCount = dict.WordCount > 0 ? $" ({dict.WordCount} words)" : "";
                var titleBlock = new TextBlock
                {
                    Text = $"{dict.Name}{badge}{wordCount}",
                    FontWeight = FontWeight.Bold
                };
                Grid.SetColumn(titleBlock, 0);

                var toggleSwitch = new CheckBox
                {
                    IsChecked = isEnabled,
                    Content = isEnabled ? "Enabled" : "Disabled"
                };
                var dictId = dict.Id;
                toggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    var checkbox = s as CheckBox;
                    if (checkbox != null && !_isInitializing())
                    {
                        OnCustomDictionaryToggled(dictId, checkbox.IsChecked == true);
                        checkbox.Content = checkbox.IsChecked == true ? "Enabled" : "Disabled";
                    }
                };
                Grid.SetColumn(toggleSwitch, 1);

                headerPanel.Children.Add(titleBlock);
                headerPanel.Children.Add(toggleSwitch);

                panel.Children.Add(headerPanel);

                if (!string.IsNullOrWhiteSpace(dict.Description))
                {
                    var descBlock = new TextBlock
                    {
                        Text = dict.Description,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontSize = 11,
                        Foreground = Brushes.Gray
                    };
                    panel.Children.Add(descBlock);
                }

                var border = new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(2),
                    Child = panel
                };

                dictionaryItems.Add(border);
            }

            if (dictionaryItems.Count == 0)
            {
                var emptyText = new TextBlock
                {
                    Text = "No custom dictionaries found. Add .dic files to the Dictionaries folder.",
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(5)
                };
                dictionaryItems.Add(emptyText);
            }

            dictionariesListBox.ItemsSource = dictionaryItems;
        }

        private void LoadDictionaryPathInfo()
        {
            var pathInfo = _window.FindControl<TextBlock>("DictionaryPathInfo");
            if (pathInfo == null)
                return;

            var dictPath = DictionaryDiscovery.GetDefaultUserDictionaryPath();
            var exists = System.IO.Directory.Exists(dictPath);

            pathInfo.Text = $"To add Hunspell dictionaries, create a folder with the language code (e.g., es_ES) " +
                           $"and place the .dic and .aff files inside.\n\n" +
                           $"Dictionary folder: ~/Radoub/Dictionaries/ {(exists ? "✅" : "(will be created)")}";
        }

        public void OnPrimaryLanguageChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing()) return;

            var comboBox = sender as ComboBox;
            var selectedItem = comboBox?.SelectedItem as LanguageListItem;

            if (selectedItem != null)
            {
                DictionarySettingsService.Instance.PrimaryLanguage = selectedItem.Id;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Primary language changed to {selectedItem.Id}");
            }
        }

        private void OnCustomDictionaryToggled(string dictionaryId, bool enabled)
        {
            if (_isInitializing()) return;

            DictionarySettingsService.Instance.SetCustomDictionaryEnabled(dictionaryId, enabled);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Dictionary {dictionaryId} {(enabled ? "enabled" : "disabled")}");
        }

        public void OnOpenDictionariesFolderClick(object? sender, RoutedEventArgs e)
        {
            var dictPath = DictionaryDiscovery.GetDefaultUserDictionaryPath();

            if (!System.IO.Directory.Exists(dictPath))
            {
                System.IO.Directory.CreateDirectory(dictPath);
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dictPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open dictionaries folder: {ex.Message}");
            }
        }

        public void OnRefreshDictionariesClick(object? sender, RoutedEventArgs e)
        {
            _dictionaryDiscovery?.ClearCache();
            LoadPrimaryLanguageList();
            LoadCustomDictionaryList();
            UnifiedLogger.LogApplication(LogLevel.INFO, "Dictionary list refreshed");
        }

        /// <summary>
        /// Helper class for language dropdown items.
        /// </summary>
        private class LanguageListItem
        {
            public string Id { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public DictionaryInfo? Info { get; set; }
        }
    }
}
