using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace DialogEditor.Views
{
    /// <summary>
    /// MainWindow partial class for theme handling and application.
    /// Extracted from MainWindow.axaml.cs for maintainability (#535).
    /// Theme/font settings now managed by RadoubSettings (Trebuchet is sole authority).
    /// </summary>
    public partial class MainWindow
    {
        private void ApplySavedTheme()
        {
            try
            {
                // Theme is now managed by ThemeManager via RadoubSettings
                // ThemeManager applies the shared theme on startup
                var themeManager = ThemeManager.Instance;
                var currentTheme = themeManager.CurrentTheme;
                bool isDark = currentTheme?.Plugin.Id.Contains("dark", StringComparison.OrdinalIgnoreCase) ?? false;
                UpdateThemeMenuChecks(isDark);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Applied theme from RadoubSettings: {currentTheme?.Plugin.Name ?? "default"}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error applying saved theme: {ex.Message}");
            }
        }

        private void OnLightThemeClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (Application.Current != null)
                {
                    Application.Current.RequestedThemeVariant = ThemeVariant.Light;
                    _viewModel.StatusMessage = "Light theme applied";
                    UpdateThemeMenuChecks(false);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error applying light theme: {ex.Message}");
            }
        }

        private void OnDarkThemeClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (Application.Current != null)
                {
                    Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                    _viewModel.StatusMessage = "Dark theme applied";
                    UpdateThemeMenuChecks(true);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error applying dark theme: {ex.Message}");
            }
        }

        private void UpdateThemeMenuChecks(bool isDark)
        {
            var lightMenuItem = this.FindControl<MenuItem>("LightThemeMenuItem");
            var darkMenuItem = this.FindControl<MenuItem>("DarkThemeMenuItem");

            if (lightMenuItem != null && darkMenuItem != null)
            {
                // Update checkbox visibility in menu icons
                // This is simplified - proper implementation would update the CheckBox IsChecked in the Icon
                _viewModel.StatusMessage = isDark ? "Dark theme" : "Light theme";
            }
        }

        /// <summary>
        /// Handler for theme changes - refreshes tree view to update node colors
        /// </summary>
        private void OnThemeApplied(object? sender, EventArgs e)
        {
            // Only refresh if a dialog is loaded
            if (_viewModel.CurrentDialog != null)
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _viewModel.RefreshTreeViewColors();
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Tree view refreshed after theme change");
                });
            }
        }

        /// <summary>
        /// Gets the theme-aware success brush for validation feedback.
        /// </summary>
        private IBrush GetSuccessBrush() => BrushManager.GetSuccessBrush(this);
    }
}
