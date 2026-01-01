using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using DialogEditor.Services;

namespace DialogEditor.Views.Controllers
{
    /// <summary>
    /// Controller for Auto-Save settings section in SettingsWindow.
    /// Handles: Auto-save enabled state, interval configuration.
    /// </summary>
    public class AutoSaveSettingsController
    {
        private readonly Window _window;
        private readonly Func<bool> _isInitializing;

        public AutoSaveSettingsController(Window window, Func<bool> isInitializing)
        {
            _window = window;
            _isInitializing = isInitializing;
        }

        public void LoadSettings()
        {
            var settings = SettingsService.Instance;

            var autoSaveEnabledCheckBox = _window.FindControl<CheckBox>("AutoSaveEnabledCheckBox");
            var autoSaveIntervalSlider = _window.FindControl<Slider>("AutoSaveIntervalSlider");
            var autoSaveIntervalLabel = _window.FindControl<TextBlock>("AutoSaveIntervalLabel");

            if (autoSaveEnabledCheckBox != null)
            {
                autoSaveEnabledCheckBox.IsChecked = settings.AutoSaveEnabled;
            }

            if (autoSaveIntervalSlider != null)
            {
                autoSaveIntervalSlider.Value = settings.AutoSaveIntervalMinutes;
            }

            if (autoSaveIntervalLabel != null)
            {
                int value = settings.AutoSaveIntervalMinutes;
                autoSaveIntervalLabel.Text = value == 0 ? "Fast debounce (2s)" : $"Every {value} minute{(value > 1 ? "s" : "")}";
            }
        }

        public void ApplySettings()
        {
            var settings = SettingsService.Instance;

            var autoSaveEnabledCheckBox = _window.FindControl<CheckBox>("AutoSaveEnabledCheckBox");
            var autoSaveIntervalSlider = _window.FindControl<Slider>("AutoSaveIntervalSlider");

            if (autoSaveEnabledCheckBox != null)
            {
                settings.AutoSaveEnabled = autoSaveEnabledCheckBox.IsChecked ?? true;
            }

            if (autoSaveIntervalSlider != null)
            {
                settings.AutoSaveIntervalMinutes = (int)autoSaveIntervalSlider.Value;
            }
        }

        public void OnAutoSaveEnabledChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing()) return;
            // Auto-save enabled change is handled in ApplySettings
        }

        public void OnAutoSaveIntervalChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            var label = _window.FindControl<TextBlock>("AutoSaveIntervalLabel");
            if (label != null && sender is Slider slider)
            {
                int value = (int)slider.Value;
                if (value == 0)
                {
                    label.Text = "Fast debounce (2s)";
                }
                else
                {
                    label.Text = $"Every {value} minute{(value > 1 ? "s" : "")}";
                }
            }
        }
    }
}
