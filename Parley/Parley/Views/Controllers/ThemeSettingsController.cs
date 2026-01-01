using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using ThemeManager = Radoub.UI.Services.ThemeManager;
using ThemeManifest = Radoub.UI.Models.ThemeManifest;

namespace DialogEditor.Views.Controllers
{
    /// <summary>
    /// Controller for Theme settings section in SettingsWindow.
    /// Handles: Theme selection, preview, description, easter egg themes.
    /// </summary>
    public class ThemeSettingsController
    {
        private readonly Window _window;
        private readonly Func<bool> _isInitializing;
        private readonly Func<IBrush> _getErrorBrush;
        private bool _easterEggActivated = false;

        public ThemeSettingsController(
            Window window,
            Func<bool> isInitializing,
            Func<IBrush> getErrorBrush)
        {
            _window = window;
            _isInitializing = isInitializing;
            _getErrorBrush = getErrorBrush;
        }

        public void LoadSettings()
        {
            var settings = SettingsService.Instance;
            var themeComboBox = _window.FindControl<ComboBox>("ThemeComboBox");

            if (themeComboBox != null)
            {
                // Populate theme list (hide easter eggs initially)
                PopulateThemeList(themeComboBox, includeEasterEggs: false);

                // Select current theme
                var themes = (IEnumerable<ThemeManifest>?)themeComboBox.ItemsSource;
                var currentTheme = themes?.FirstOrDefault(t => t.Plugin.Id == settings.CurrentThemeId);
                themeComboBox.SelectedItem = currentTheme;

                // Update theme description
                if (currentTheme != null)
                {
                    UpdateThemeDescription(currentTheme);
                }
            }

            // Apply current theme immediately when dialog opens
            ApplyThemePreview();
        }

        public void OnThemeComboBoxChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing()) return;

            var comboBox = sender as ComboBox;
            if (comboBox?.SelectedItem is ThemeManifest theme)
            {
                // Apply theme immediately
                ThemeManager.Instance.ApplyTheme(theme);

                // Update theme description panel
                UpdateThemeDescription(theme);

                // Save to settings
                SettingsService.Instance.CurrentThemeId = theme.Plugin.Id;

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Theme changed to: {theme.Plugin.Name}");
            }
        }

        public void OnGetThemesClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                const string themesUrl = "https://github.com/LordOfMyatar/Radoub/tree/main/Parley/Parley/Themes";

                var psi = new ProcessStartInfo
                {
                    FileName = themesUrl,
                    UseShellExecute = true
                };
                Process.Start(psi);

                UnifiedLogger.LogApplication(LogLevel.INFO, "Opened GitHub themes directory");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open themes URL: {ex.Message}");
            }
        }

        public void OnEasterEggHintClick(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (_easterEggActivated) return;

            _easterEggActivated = true;

            var comboBox = _window.FindControl<ComboBox>("ThemeComboBox");
            if (comboBox == null) return;

            // Repopulate list with easter eggs
            PopulateThemeList(comboBox, includeEasterEggs: true);

            // Select the easter egg theme
            var easterEggs = (IEnumerable<ThemeManifest>?)comboBox.ItemsSource;
            var easterEgg = easterEggs?.FirstOrDefault(t => t.Plugin.Tags.Contains("easter-egg"));

            if (easterEgg != null)
            {
                comboBox.SelectedItem = easterEgg;
            }

            // Update easter egg hint
            var hint = _window.FindControl<TextBlock>("EasterEggHint");
            if (hint != null)
            {
                hint.Text = "🍇🍓 You found it! Enjoy the chaos...";
                hint.Foreground = Avalonia.Media.Brushes.DarkOrange;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Easter egg theme activated!");
        }

        private void UpdateThemeDescription(ThemeManifest theme)
        {
            var nameText = _window.FindControl<TextBlock>("ThemeNameText");
            var descText = _window.FindControl<TextBlock>("ThemeDescriptionText");
            var accessText = _window.FindControl<TextBlock>("ThemeAccessibilityText");

            if (nameText != null)
            {
                nameText.Text = theme.Plugin.Name;
            }

            if (descText != null)
            {
                descText.Text = theme.Plugin.Description;
            }

            if (accessText != null && theme.Accessibility != null)
            {
                if (theme.Accessibility.Type == "colorblind")
                {
                    var condition = theme.Accessibility.Condition ?? "color blindness";
                    accessText.Text = $"♿ Accessibility: Optimized for {condition} ({theme.Accessibility.ContrastLevel} contrast)";
                    accessText.IsVisible = true;
                }
                else if (theme.Accessibility.Type == "nightmare")
                {
                    accessText.Text = theme.Accessibility.Warning ?? "⚠️ Warning: This theme may cause eye strain";
                    accessText.Foreground = _getErrorBrush();
                    accessText.IsVisible = true;
                }
                else
                {
                    accessText.IsVisible = false;
                }
            }
        }

        private void PopulateThemeList(ComboBox comboBox, bool includeEasterEggs)
        {
            var themes = ThemeManager.Instance.AvailableThemes;

            if (!includeEasterEggs)
            {
                themes = themes.Where(t => !t.Plugin.Tags.Contains("easter-egg")).ToList();
            }

            var sortedThemes = themes
                .OrderBy(t => t.Accessibility?.Type == "colorblind" ? 1 : 0)
                .ThenBy(t => t.Plugin.Name)
                .ToList();

            comboBox.ItemsSource = sortedThemes;
            comboBox.DisplayMemberBinding = new Binding("Plugin.Name");
        }

        private void ApplyThemePreview()
        {
            // Theme preview now handled by OnThemeComboBoxChanged
            // This method kept for compatibility but is no longer used
        }
    }
}
