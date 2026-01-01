using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Views.Controllers
{
    /// <summary>
    /// Controller for Parameter Cache settings section in SettingsWindow.
    /// Handles: Cache enable/disable, max values, max scripts, cache stats, clear cache.
    /// </summary>
    public class ParameterCacheController
    {
        private readonly Window _window;
        private readonly Func<bool> _isInitializing;

        public ParameterCacheController(Window window, Func<bool> isInitializing)
        {
            _window = window;
            _isInitializing = isInitializing;
        }

        public void LoadSettings()
        {
            var settings = SettingsService.Instance;

            var enableParameterCacheCheckBox = _window.FindControl<CheckBox>("EnableParameterCacheCheckBox");
            var maxCachedValuesSlider = _window.FindControl<Slider>("MaxCachedValuesSlider");
            var maxCachedValuesLabel = _window.FindControl<TextBlock>("MaxCachedValuesLabel");
            var maxCachedScriptsSlider = _window.FindControl<Slider>("MaxCachedScriptsSlider");
            var maxCachedScriptsLabel = _window.FindControl<TextBlock>("MaxCachedScriptsLabel");

            if (enableParameterCacheCheckBox != null)
            {
                enableParameterCacheCheckBox.IsChecked = settings.EnableParameterCache;
            }

            if (maxCachedValuesSlider != null)
            {
                maxCachedValuesSlider.Value = settings.MaxCachedValuesPerParameter;
            }

            if (maxCachedValuesLabel != null)
            {
                maxCachedValuesLabel.Text = $"{settings.MaxCachedValuesPerParameter} values";
            }

            if (maxCachedScriptsSlider != null)
            {
                maxCachedScriptsSlider.Value = settings.MaxCachedScripts;
            }

            if (maxCachedScriptsLabel != null)
            {
                maxCachedScriptsLabel.Text = $"{settings.MaxCachedScripts} scripts";
            }

            UpdateCacheStats();
        }

        public void OnParameterCacheSettingChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing()) return;

            var checkbox = sender as CheckBox;
            if (checkbox != null)
            {
                SettingsService.Instance.EnableParameterCache = checkbox.IsChecked ?? true;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Parameter cache {(checkbox.IsChecked == true ? "enabled" : "disabled")}");
            }
        }

        public void OnMaxCachedValuesChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isInitializing()) return;

            var slider = sender as Slider;
            var label = _window.FindControl<TextBlock>("MaxCachedValuesLabel");

            if (slider != null && label != null)
            {
                int value = (int)slider.Value;
                label.Text = $"{value} values";
                SettingsService.Instance.MaxCachedValuesPerParameter = value;
            }
        }

        public void OnMaxCachedScriptsChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isInitializing()) return;

            var slider = sender as Slider;
            var label = _window.FindControl<TextBlock>("MaxCachedScriptsLabel");

            if (slider != null && label != null)
            {
                int value = (int)slider.Value;
                label.Text = $"{value} scripts";
                SettingsService.Instance.MaxCachedScripts = value;
            }
        }

        public void UpdateCacheStats()
        {
            try
            {
                var stats = ParameterCacheService.Instance.GetStats();
                var statsText = _window.FindControl<TextBlock>("CacheStatsText");

                if (statsText != null)
                {
                    statsText.Text = $"Cached Scripts: {stats.ScriptCount}\n" +
                                   $"Total Parameters: {stats.ParameterCount}\n" +
                                   $"Total Values: {stats.ValueCount}";
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error updating cache stats: {ex.Message}");
            }
        }

        public async void OnClearParameterCacheClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var result = await ShowConfirmationAsync("Clear Parameter Cache",
                    "This will delete all cached parameter values. Are you sure?");

                if (result)
                {
                    ParameterCacheService.Instance.ClearAllCache();
                    UpdateCacheStats();
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Parameter cache cleared");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error clearing parameter cache: {ex.Message}");
            }
        }

        public void OnRefreshCacheStatsClick(object? sender, RoutedEventArgs e)
        {
            UpdateCacheStats();
        }

        private async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var result = false;
            var panel = new StackPanel { Margin = new Thickness(20), Spacing = 15 };

            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };

            var yesButton = new Button { Content = "Yes", Width = 80 };
            yesButton.Click += (s, e) => { result = true; dialog.Close(); };

            var noButton = new Button { Content = "No", Width = 80 };
            noButton.Click += (s, e) => { result = false; dialog.Close(); };

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
            panel.Children.Add(buttonPanel);

            dialog.Content = panel;
            await dialog.ShowDialog(_window);

            return result;
        }
    }
}
