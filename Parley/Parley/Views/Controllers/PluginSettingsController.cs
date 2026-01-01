using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using DialogEditor.Plugins;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Views.Controllers
{
    /// <summary>
    /// Controller for Plugin settings section in SettingsWindow.
    /// Handles: Plugin listing, enable/disable, safe mode, folder operations.
    /// </summary>
    public class PluginSettingsController
    {
        private readonly Window _window;
        private readonly Func<bool> _isInitializing;
        private readonly PluginManager? _pluginManager;

        public PluginSettingsController(
            Window window,
            Func<bool> isInitializing,
            PluginManager? pluginManager)
        {
            _window = window;
            _isInitializing = isInitializing;
            _pluginManager = pluginManager;
        }

        public void LoadSettings()
        {
            var safeModeCheckBox = _window.FindControl<CheckBox>("SafeModeCheckBox");
            if (safeModeCheckBox != null)
            {
                safeModeCheckBox.IsChecked = PluginSettingsService.Instance.SafeMode;
            }

            LoadPluginList();
        }

        public void LoadPluginList()
        {
            var pluginsListBox = _window.FindControl<ListBox>("PluginsListBox");
            if (pluginsListBox == null || _pluginManager == null)
                return;

            // Scan for plugins
            _pluginManager.Discovery.ScanForPlugins();

            var pluginSettings = PluginSettingsService.Instance;
            var pluginItems = new List<Control>();

            foreach (var discoveredPlugin in _pluginManager.Discovery.DiscoveredPlugins)
            {
                var manifest = discoveredPlugin.Manifest;
                var permissions = manifest.Permissions != null && manifest.Permissions.Count > 0
                    ? string.Join(", ", manifest.Permissions)
                    : "none";

                var isEnabled = pluginSettings.IsPluginEnabled(manifest.Plugin.Id);
                var trustBadge = manifest.TrustLevel.ToUpperInvariant() switch
                {
                    "OFFICIAL" => "[OFFICIAL]",
                    "VERIFIED" => "[VERIFIED]",
                    _ => "[UNVERIFIED]"
                };

                // Create item UI directly
                var panel = new StackPanel
                {
                    Spacing = 5,
                    Margin = new Thickness(5)
                };

                var headerPanel = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto")
                };

                var titleBlock = new TextBlock
                {
                    Text = $"{manifest.Plugin.Name} v{manifest.Plugin.Version} by {manifest.Plugin.Author} {trustBadge}",
                    FontWeight = FontWeight.Bold
                };
                Grid.SetColumn(titleBlock, 0);

                var toggleSwitch = new CheckBox
                {
                    IsChecked = isEnabled,
                    Content = isEnabled ? "Enabled" : "Disabled"
                };
                var pluginId = manifest.Plugin.Id;
                toggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    var checkbox = s as CheckBox;
                    if (checkbox != null && !_isInitializing())
                    {
                        OnPluginToggled(pluginId, checkbox.IsChecked == true);
                        checkbox.Content = checkbox.IsChecked == true ? "Enabled" : "Disabled";
                    }
                };
                Grid.SetColumn(toggleSwitch, 1);

                headerPanel.Children.Add(titleBlock);
                headerPanel.Children.Add(toggleSwitch);

                var descBlock = new TextBlock
                {
                    Text = manifest.Plugin.Description ?? "",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    FontSize = 11,
                    Foreground = Brushes.Gray
                };

                var permBlock = new TextBlock
                {
                    Text = $"Permissions: {permissions}",
                    FontSize = 11,
                    Foreground = Brushes.DarkGray
                };

                panel.Children.Add(headerPanel);
                panel.Children.Add(descBlock);
                panel.Children.Add(permBlock);

                var border = new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(2),
                    Child = panel
                };

                pluginItems.Add(border);
            }

            pluginsListBox.ItemsSource = pluginItems;
        }

        private void OnPluginToggled(string pluginId, bool enabled)
        {
            if (_isInitializing()) return;

            PluginSettingsService.Instance.SetPluginEnabled(pluginId, enabled);

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Plugin {pluginId} {(enabled ? "enabled" : "disabled")}");
        }

        public void OnSafeModeChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing()) return;

            var safeModeCheckBox = _window.FindControl<CheckBox>("SafeModeCheckBox");
            if (safeModeCheckBox != null)
            {
                PluginSettingsService.Instance.SafeMode = safeModeCheckBox.IsChecked == true;
            }
        }

        public void OnOpenPluginsFolderClick(object? sender, RoutedEventArgs e)
        {
            var userDataDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub", "Parley",
                "Plugins",
                "Community"
            );

            if (!System.IO.Directory.Exists(userDataDir))
            {
                System.IO.Directory.CreateDirectory(userDataDir);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Created plugins directory: {userDataDir}");
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("explorer.exe", userDataDir);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", userDataDir);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", userDataDir);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open plugins folder: {ex.Message}");
            }
        }

        public void OnRefreshPluginsClick(object? sender, RoutedEventArgs e)
        {
            LoadPluginList();
            UnifiedLogger.LogApplication(LogLevel.INFO, "Plugin list refreshed");
        }
    }
}
