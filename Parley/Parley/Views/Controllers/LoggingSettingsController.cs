using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using DialogEditor.Services;

namespace DialogEditor.Views.Controllers
{
    /// <summary>
    /// Controller for Debug settings section in SettingsWindow.
    /// Handles: Debug panel visibility.
    /// Note: Log level and retention are now centralized in Trebuchet (RadoubSettings).
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

            var showDebugPanelCheckBox = _window.FindControl<CheckBox>("ShowDebugPanelCheckBox");

            if (showDebugPanelCheckBox != null)
            {
                showDebugPanelCheckBox.IsChecked = settings.DebugWindowVisible;
            }
        }

        public void ApplySettings()
        {
            var settings = SettingsService.Instance;

            var showDebugPanelCheckBox = _window.FindControl<CheckBox>("ShowDebugPanelCheckBox");

            if (showDebugPanelCheckBox != null)
            {
                settings.DebugWindowVisible = showDebugPanelCheckBox.IsChecked ?? false;
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
