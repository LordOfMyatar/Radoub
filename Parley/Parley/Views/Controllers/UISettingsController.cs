using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Views.Controllers
{
    /// <summary>
    /// Controller for UI/Appearance settings section in SettingsWindow.
    /// Handles: Font size, font family, scrollbar, NPC coloring, warnings, spell check.
    /// </summary>
    public class UISettingsController
    {
        private readonly Window _window;
        private readonly Func<bool> _isInitializing;

        public UISettingsController(Window window, Func<bool> isInitializing)
        {
            _window = window;
            _isInitializing = isInitializing;
        }

        public void LoadSettings()
        {
            var settings = SettingsService.Instance;

            var fontSizeSlider = _window.FindControl<Slider>("FontSizeSlider");
            var fontSizeLabel = _window.FindControl<TextBlock>("FontSizeLabel");

            if (fontSizeSlider != null)
            {
                fontSizeSlider.Value = settings.FontSize;
            }

            if (fontSizeLabel != null)
            {
                fontSizeLabel.Text = settings.FontSize.ToString("0");
            }

            LoadFontFamilies(settings.FontFamily);
            UpdateFontPreview();

            var allowScrollbarAutoHideCheckBox = _window.FindControl<CheckBox>("AllowScrollbarAutoHideCheckBox");
            if (allowScrollbarAutoHideCheckBox != null)
            {
                allowScrollbarAutoHideCheckBox.IsChecked = settings.AllowScrollbarAutoHide;
            }

            var enableNpcTagColoringCheckBox = _window.FindControl<CheckBox>("EnableNpcTagColoringCheckBox");
            if (enableNpcTagColoringCheckBox != null)
            {
                enableNpcTagColoringCheckBox.IsChecked = settings.EnableNpcTagColoring;
            }

            var simulatorShowWarningsCheckBox = _window.FindControl<CheckBox>("SimulatorShowWarningsCheckBox");
            if (simulatorShowWarningsCheckBox != null)
            {
                simulatorShowWarningsCheckBox.IsChecked = settings.SimulatorShowWarnings;
            }

            var spellCheckEnabledCheckBox = _window.FindControl<CheckBox>("SpellCheckEnabledCheckBox");
            if (spellCheckEnabledCheckBox != null)
            {
                spellCheckEnabledCheckBox.IsChecked = settings.SpellCheckEnabled;
            }

            var externalEditorPathTextBox = _window.FindControl<TextBox>("ExternalEditorPathTextBox");
            if (externalEditorPathTextBox != null)
            {
                externalEditorPathTextBox.Text = settings.ExternalEditorPath;
            }
        }

        public void ApplySettings()
        {
            var settings = SettingsService.Instance;

            var fontSizeSlider = _window.FindControl<Slider>("FontSizeSlider");
            var externalEditorPathTextBox = _window.FindControl<TextBox>("ExternalEditorPathTextBox");

            if (fontSizeSlider != null)
            {
                settings.FontSize = fontSizeSlider.Value;
            }

            var fontFamilyComboBox = _window.FindControl<ComboBox>("FontFamilyComboBox");
            if (fontFamilyComboBox?.SelectedItem is string selectedFont)
            {
                settings.FontFamily = selectedFont == "System Default" ? "" : selectedFont;
            }

            if (externalEditorPathTextBox != null)
            {
                settings.ExternalEditorPath = externalEditorPathTextBox.Text ?? "";
            }
        }

        public void OnFontSizeChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            var fontSizeLabel = _window.FindControl<TextBlock>("FontSizeLabel");
            if (fontSizeLabel != null && sender is Slider slider)
            {
                fontSizeLabel.Text = slider.Value.ToString("0");
            }

            if (!_isInitializing())
            {
                ApplyFontSizePreview();
                UpdateFontPreview();
            }
        }

        public void OnFontFamilyChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing()) return;

            var fontFamilyComboBox = sender as ComboBox;
            if (fontFamilyComboBox?.SelectedItem is string selectedFont)
            {
                if (selectedFont == "System Default")
                {
                    App.ApplyFontFamily("");
                }
                else
                {
                    App.ApplyFontFamily(selectedFont);
                }

                UpdateFontPreview();
            }
        }

        public void OnAllowScrollbarAutoHideChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing()) return;

            var checkbox = sender as CheckBox;
            if (checkbox != null)
            {
                SettingsService.Instance.AllowScrollbarAutoHide = checkbox.IsChecked == true;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Scrollbar auto-hide preference: {checkbox.IsChecked}");
            }
        }

        public void OnEnableNpcTagColoringChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing()) return;

            var checkbox = sender as CheckBox;
            if (checkbox != null)
            {
                SettingsService.Instance.EnableNpcTagColoring = checkbox.IsChecked == true;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"NPC tag coloring: {(checkbox.IsChecked == true ? "enabled" : "disabled")}");
            }
        }

        public void OnSimulatorShowWarningsChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing()) return;

            var checkbox = sender as CheckBox;
            if (checkbox != null)
            {
                SettingsService.Instance.SimulatorShowWarnings = checkbox.IsChecked == true;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Dialog warnings: {(checkbox.IsChecked == true ? "enabled" : "disabled")}");
            }
        }

        public void OnSpellCheckEnabledChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing()) return;

            var checkbox = sender as CheckBox;
            if (checkbox != null)
            {
                SettingsService.Instance.SpellCheckEnabled = checkbox.IsChecked == true;
            }
        }

        private void LoadFontFamilies(string currentFontFamily)
        {
            try
            {
                var fontFamilyComboBox = _window.FindControl<ComboBox>("FontFamilyComboBox");
                if (fontFamilyComboBox == null) return;

                var fonts = new ObservableCollection<string> { "System Default" };

                var commonFonts = new[]
                {
                    "Arial", "Calibri", "Cambria", "Consolas", "Courier New",
                    "Georgia", "Helvetica", "Segoe UI", "Tahoma", "Times New Roman",
                    "Trebuchet MS", "Verdana",
                    "San Francisco", "Ubuntu", "Noto Sans", "Roboto"
                };

                foreach (var font in commonFonts)
                {
                    try
                    {
                        var testFamily = new FontFamily(font);
                        fonts.Add(font);
                    }
                    catch
                    {
                        // Font not available on this system
                    }
                }

                fontFamilyComboBox.ItemsSource = fonts;

                if (string.IsNullOrWhiteSpace(currentFontFamily))
                {
                    fontFamilyComboBox.SelectedIndex = 0;
                }
                else
                {
                    var index = fonts.IndexOf(currentFontFamily);
                    fontFamilyComboBox.SelectedIndex = index >= 0 ? index : 0;
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading font families: {ex.Message}");
            }
        }

        public void UpdateFontPreview()
        {
            try
            {
                var fontPreviewText = _window.FindControl<TextBlock>("FontPreviewText");
                var fontFamilyComboBox = _window.FindControl<ComboBox>("FontFamilyComboBox");
                var fontSizeSlider = _window.FindControl<Slider>("FontSizeSlider");

                if (fontPreviewText != null)
                {
                    if (fontSizeSlider != null)
                    {
                        fontPreviewText.FontSize = fontSizeSlider.Value;
                    }

                    if (fontFamilyComboBox?.SelectedItem is string selectedFont)
                    {
                        if (selectedFont == "System Default")
                        {
                            fontPreviewText.FontFamily = FontFamily.Default;
                        }
                        else
                        {
                            try
                            {
                                fontPreviewText.FontFamily = new FontFamily(selectedFont);
                            }
                            catch (Exception ex)
                            {
                                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Font '{selectedFont}' not available for preview, using default: {ex.Message}");
                                fontPreviewText.FontFamily = FontFamily.Default;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error updating font preview: {ex.Message}");
            }
        }

        private void ApplyFontSizePreview()
        {
            try
            {
                var fontSizeSlider = _window.FindControl<Slider>("FontSizeSlider");
                if (fontSizeSlider != null)
                {
                    App.ApplyFontSize(fontSizeSlider.Value);
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Font size preview: {fontSizeSlider.Value}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error applying font size preview: {ex.Message}");
            }
        }
    }
}
