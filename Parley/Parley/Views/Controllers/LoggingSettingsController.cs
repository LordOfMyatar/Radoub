using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Views.Controllers
{
    /// <summary>
    /// Controller for Logging settings section in SettingsWindow.
    /// Handles: Log level, retention, debug panel visibility.
    /// </summary>
    public class LoggingSettingsController
    {
        private readonly Window _window;
        private readonly Func<bool> _isInitializing;

        public LoggingSettingsController(Window window, Func<bool> isInitializing)
        {
            _window = window;
            _isInitializing = isInitializing;
        }

        public void LoadSettings()
        {
            var settings = SettingsService.Instance;

            var logLevelComboBox = _window.FindControl<ComboBox>("LogLevelComboBox");
            var logRetentionSlider = _window.FindControl<Slider>("LogRetentionSlider");
            var logRetentionLabel = _window.FindControl<TextBlock>("LogRetentionLabel");
            var showDebugPanelCheckBox = _window.FindControl<CheckBox>("ShowDebugPanelCheckBox");

            if (logLevelComboBox != null)
            {
                logLevelComboBox.ItemsSource = Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>().ToList();
                logLevelComboBox.SelectedItem = settings.CurrentLogLevel;
            }

            if (logRetentionSlider != null)
            {
                logRetentionSlider.Value = settings.LogRetentionSessions;
            }

            if (logRetentionLabel != null)
            {
                logRetentionLabel.Text = $"{settings.LogRetentionSessions} sessions";
            }

            if (showDebugPanelCheckBox != null)
            {
                showDebugPanelCheckBox.IsChecked = settings.DebugWindowVisible;
            }
        }

        public void ApplySettings()
        {
            var settings = SettingsService.Instance;

            var logLevelComboBox = _window.FindControl<ComboBox>("LogLevelComboBox");
            var logRetentionSlider = _window.FindControl<Slider>("LogRetentionSlider");
            var showDebugPanelCheckBox = _window.FindControl<CheckBox>("ShowDebugPanelCheckBox");

            if (logLevelComboBox?.SelectedItem is LogLevel logLevel)
            {
                settings.CurrentLogLevel = logLevel;
            }

            if (logRetentionSlider != null)
            {
                settings.LogRetentionSessions = (int)logRetentionSlider.Value;
            }

            if (showDebugPanelCheckBox != null)
            {
                settings.DebugWindowVisible = showDebugPanelCheckBox.IsChecked ?? false;
            }
        }

        public void OnLogLevelChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing()) return;
            // Log level change is handled in ApplySettings
        }

        public void OnLogRetentionChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            var logRetentionLabel = _window.FindControl<TextBlock>("LogRetentionLabel");
            if (logRetentionLabel != null && sender is Slider slider)
            {
                logRetentionLabel.Text = $"{(int)slider.Value} sessions";
            }
        }

        public void OnShowDebugPanelChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing()) return;

            var showDebugPanelCheckBox = _window.FindControl<CheckBox>("ShowDebugPanelCheckBox");
            if (showDebugPanelCheckBox != null)
            {
                bool isVisible = showDebugPanelCheckBox.IsChecked ?? false;
                SettingsService.Instance.DebugWindowVisible = isVisible;

                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    if (desktop.MainWindow is MainWindow mainWindow)
                    {
                        var debugTab = mainWindow.FindControl<TabItem>("DebugTab");
                        if (debugTab != null)
                        {
                            debugTab.IsVisible = isVisible;
                        }
                    }
                }
            }
        }
    }
}
