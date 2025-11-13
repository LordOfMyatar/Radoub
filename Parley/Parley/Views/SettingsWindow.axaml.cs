using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DialogEditor.Plugins;
using DialogEditor.Services;

namespace DialogEditor.Views
{
    /// <summary>
    /// View model for plugin list item
    /// </summary>
    public class PluginListItemViewModel
    {
        public string PluginId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public string TrustLevel { get; set; } = "";
        public string Permissions { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
        public string TrustBadge => TrustLevel.ToUpperInvariant() switch
        {
            "OFFICIAL" => "[OFFICIAL]",
            "VERIFIED" => "[VERIFIED]",
            _ => "[UNVERIFIED]"
        };
        public string DisplayText => $"{Name} v{Version} by {Author} {TrustBadge}";
    }

    public partial class SettingsWindow : Window
    {
        private bool _isInitializing = true;
        private PluginManager? _pluginManager;

        // Parameterless constructor for XAML/Avalonia runtime
        public SettingsWindow() : this(0, null)
        {
        }

        public SettingsWindow(int initialTab = 0, PluginManager? pluginManager = null)
        {
            InitializeComponent();
            _pluginManager = pluginManager;
            LoadSettings();
            _isInitializing = false;

            // Apply current theme immediately when dialog opens
            ApplyThemePreview();

            // Select the specified tab
            var tabControl = this.FindControl<TabControl>("SettingsTabControl");
            if (tabControl != null)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"SettingsWindow: initialTab={initialTab}, ItemCount={tabControl.ItemCount}");
                if (initialTab >= 0 && initialTab < tabControl.ItemCount)
                {
                    tabControl.SelectedIndex = initialTab;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"SettingsWindow: Selected tab {initialTab}");
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"SettingsWindow: Invalid tab index {initialTab} (valid range: 0-{tabControl.ItemCount - 1})");
                }
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, "SettingsWindow: Could not find SettingsTabControl");
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void LoadSettings()
        {
            var settings = SettingsService.Instance;

            // Resource Paths
            var gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox");
            var baseGamePathTextBox = this.FindControl<TextBox>("BaseGamePathTextBox"); // Phase 2
            var modulePathTextBox = this.FindControl<TextBox>("ModulePathTextBox");
            var recentModulesListBox = this.FindControl<ListBox>("RecentModulesListBox");

            if (gamePathTextBox != null)
            {
                gamePathTextBox.Text = settings.NeverwinterNightsPath;
                // Only validate if path is not empty
                if (!string.IsNullOrEmpty(settings.NeverwinterNightsPath))
                {
                    ValidateGamePath(settings.NeverwinterNightsPath);
                }
            }

            // Phase 2: Load base game installation path
            if (baseGamePathTextBox != null)
            {
                baseGamePathTextBox.Text = settings.BaseGameInstallPath;
                if (!string.IsNullOrEmpty(settings.BaseGameInstallPath))
                {
                    ValidateBaseGamePath(settings.BaseGameInstallPath);
                }
            }

            if (modulePathTextBox != null)
            {
                modulePathTextBox.Text = settings.CurrentModulePath;
                // Only validate if path is not empty
                if (!string.IsNullOrEmpty(settings.CurrentModulePath))
                {
                    ValidateModulePath(settings.CurrentModulePath);
                }
            }

            if (recentModulesListBox != null)
            {
                recentModulesListBox.ItemsSource = settings.ModulePaths;
            }

            // UI Settings
            var lightThemeRadio = this.FindControl<RadioButton>("LightThemeRadio");
            var darkThemeRadio = this.FindControl<RadioButton>("DarkThemeRadio");
            var fontSizeSlider = this.FindControl<Slider>("FontSizeSlider");
            var fontSizeLabel = this.FindControl<TextBlock>("FontSizeLabel");
            var externalEditorPathTextBox = this.FindControl<TextBox>("ExternalEditorPathTextBox");

            if (settings.IsDarkTheme)
            {
                if (darkThemeRadio != null) darkThemeRadio.IsChecked = true;
            }
            else
            {
                if (lightThemeRadio != null) lightThemeRadio.IsChecked = true;
            }

            if (fontSizeSlider != null)
            {
                fontSizeSlider.Value = settings.FontSize;
            }

            if (fontSizeLabel != null)
            {
                fontSizeLabel.Text = settings.FontSize.ToString("0");
            }

            // Load available fonts and current font selection
            LoadFontFamilies(settings.FontFamily);
            UpdateFontPreview();

            if (externalEditorPathTextBox != null)
            {
                externalEditorPathTextBox.Text = settings.ExternalEditorPath;
            }

            // Logging Settings
            var logLevelComboBox = this.FindControl<ComboBox>("LogLevelComboBox");
            var logRetentionSlider = this.FindControl<Slider>("LogRetentionSlider");
            var logRetentionLabel = this.FindControl<TextBlock>("LogRetentionLabel");

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

            // Parameter Cache Settings
            var enableParameterCacheCheckBox = this.FindControl<CheckBox>("EnableParameterCacheCheckBox");
            var maxCachedValuesSlider = this.FindControl<Slider>("MaxCachedValuesSlider");
            var maxCachedValuesLabel = this.FindControl<TextBlock>("MaxCachedValuesLabel");
            var maxCachedScriptsSlider = this.FindControl<Slider>("MaxCachedScriptsSlider");
            var maxCachedScriptsLabel = this.FindControl<TextBlock>("MaxCachedScriptsLabel");

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

            // Load cache statistics
            UpdateCacheStats();

            // Platform-specific paths info
            var platformPathsInfo = this.FindControl<TextBlock>("PlatformPathsInfo");
            if (platformPathsInfo != null)
            {
                platformPathsInfo.Text = GetPlatformPathsInfo();
            }

            // Plugin settings
            var safeModeCheckBox = this.FindControl<CheckBox>("SafeModeCheckBox");
            if (safeModeCheckBox != null)
            {
                safeModeCheckBox.IsChecked = PluginSettingsService.Instance.SafeMode;
            }

            LoadPluginList();
        }

        private void LoadPluginList()
        {
            var pluginsListBox = this.FindControl<ListBox>("PluginsListBox");
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
                var pluginId = manifest.Plugin.Id; // Capture for lambda
                toggleSwitch.IsCheckedChanged += (s, e) =>
                {
                    var checkbox = s as CheckBox;
                    if (checkbox != null && !_isInitializing)
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
            if (_isInitializing) return;

            PluginSettingsService.Instance.SetPluginEnabled(pluginId, enabled);

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Plugin {pluginId} {(enabled ? "enabled" : "disabled")}");
        }

        private string GetPlatformPathsInfo()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var gamePath = System.IO.Path.Combine(documents, "Neverwinter Nights");
                var modulePath = System.IO.Path.Combine(documents, "Neverwinter Nights", "modules");

                var gameExists = System.IO.Directory.Exists(gamePath);
                var moduleExists = System.IO.Directory.Exists(modulePath);

                return $"Windows (Expected Locations):\n" +
                       $"User Data: {gamePath} {(gameExists ? "✅" : "❌")}\n" +
                       $"Modules: {modulePath} {(moduleExists ? "✅" : "❌")}";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var gamePath = System.IO.Path.Combine(userProfile, "Library", "Application Support", "Neverwinter Nights");
                var modulePath = System.IO.Path.Combine(userProfile, "Library", "Application Support", "Neverwinter Nights", "modules");

                var gameExists = System.IO.Directory.Exists(gamePath);
                var moduleExists = System.IO.Directory.Exists(modulePath);

                return $"macOS (Expected Locations):\n" +
                       $"User Data: {gamePath} {(gameExists ? "✅" : "❌")}\n" +
                       $"Modules: {modulePath} {(moduleExists ? "✅" : "❌")}";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var gamePath = System.IO.Path.Combine(userProfile, ".local", "share", "Neverwinter Nights");
                var modulePath = System.IO.Path.Combine(userProfile, ".local", "share", "Neverwinter Nights", "modules");

                var gameExists = System.IO.Directory.Exists(gamePath);
                var moduleExists = System.IO.Directory.Exists(modulePath);

                return $"Linux (Expected Locations):\n" +
                       $"User Data: {gamePath} {(gameExists ? "✅" : "❌")}\n" +
                       $"Modules: {modulePath} {(moduleExists ? "✅" : "❌")}";
            }

            return "Unknown platform";
        }

        private async void OnBrowseGamePathClick(object? sender, RoutedEventArgs e)
        {
            // Clear previous validation errors
            var validation = this.FindControl<TextBlock>("GamePathValidation");
            if (validation != null)
            {
                validation.Text = "";
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Browse User Data Directory clicked");

            var storageProvider = StorageProvider;
            if (storageProvider == null) return;

            // Default to Documents folder (user data location)
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var defaultPath = System.IO.Path.Combine(documentsPath, "Neverwinter Nights");

            var options = new FolderPickerOpenOptions
            {
                Title = "Select Neverwinter Nights User Data Directory (Documents\\Neverwinter Nights)",
                AllowMultiple = false,
                SuggestedStartLocation = System.IO.Directory.Exists(defaultPath)
                    ? await storageProvider.TryGetFolderFromPathAsync(new Uri(defaultPath))
                    : await storageProvider.TryGetFolderFromPathAsync(new Uri(documentsPath))
            };

            var result = await storageProvider.OpenFolderPickerAsync(options);
            if (result.Count > 0)
            {
                var path = result[0].Path.LocalPath;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"User selected game path: {path}");
                var gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox");
                if (gamePathTextBox != null)
                {
                    gamePathTextBox.Text = path;
                    ValidateGamePath(path);
                }
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Browse canceled by user");
            }
        }

        private void OnAutoDetectGamePathClick(object? sender, RoutedEventArgs e)
        {
            var detectedPath = ResourcePathHelper.AutoDetectGamePath();
            var gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox");

            if (!string.IsNullOrEmpty(detectedPath) && gamePathTextBox != null)
            {
                gamePathTextBox.Text = detectedPath;
                ValidateGamePath(detectedPath);
            }
            else
            {
                var validation = this.FindControl<TextBlock>("GamePathValidation");
                if (validation != null)
                {
                    validation.Text = "❌ Could not auto-detect game path. Please browse manually.";
                    validation.Foreground = Brushes.Red;
                }
            }
        }

        private async void OnBrowseExternalEditorClick(object? sender, RoutedEventArgs e)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Browse External Editor clicked");

            var storageProvider = StorageProvider;
            if (storageProvider == null) return;

            var options = new FilePickerOpenOptions
            {
                Title = "Select External Text Editor",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Executable Files")
                    {
                        Patterns = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                            ? new[] { "*.exe" }
                            : new[] { "*" }
                    },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            };

            var result = await storageProvider.OpenFilePickerAsync(options);
            if (result.Count > 0)
            {
                var path = result[0].Path.LocalPath;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"User selected external editor: {UnifiedLogger.SanitizePath(path)}");
                var externalEditorPathTextBox = this.FindControl<TextBox>("ExternalEditorPathTextBox");
                if (externalEditorPathTextBox != null)
                {
                    externalEditorPathTextBox.Text = path;
                }
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Browse canceled by user");
            }
        }

        // Base Game Installation Path handlers
        private async void OnBrowseBaseGamePathClick(object? sender, RoutedEventArgs e)
        {
            // Clear previous validation errors
            var validation = this.FindControl<TextBlock>("BaseGamePathValidation");
            if (validation != null)
            {
                validation.Text = "";
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Browse Base Game Installation clicked");

            var storageProvider = StorageProvider;
            if (storageProvider == null) return;

            var options = new FolderPickerOpenOptions
            {
                Title = "Select Neverwinter Nights Base Game Installation (contains data\\ folder)",
                AllowMultiple = false
            };

            var result = await storageProvider.OpenFolderPickerAsync(options);
            if (result.Count > 0)
            {
                var path = result[0].Path.LocalPath;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"User selected base game path: {path}");
                var baseGamePathTextBox = this.FindControl<TextBox>("BaseGamePathTextBox");
                if (baseGamePathTextBox != null)
                {
                    baseGamePathTextBox.Text = path;
                    ValidateBaseGamePath(path);
                }
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Browse canceled by user");
            }
        }

        private void OnAutoDetectBaseGamePathClick(object? sender, RoutedEventArgs e)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Auto-detect Base Game Installation clicked");

            // Try to detect Steam/GOG installation
            var possiblePaths = new List<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Try common Steam locations
                var steamPaths = new[]
                {
                    @"C:\Program Files (x86)\Steam\steamapps\common\Neverwinter Nights",
                    @"C:\Program Files\Steam\steamapps\common\Neverwinter Nights",
                    @"D:\SteamLibrary\steamapps\common\Neverwinter Nights",
                    @"E:\SteamLibrary\steamapps\common\Neverwinter Nights"
                };
                possiblePaths.AddRange(steamPaths);

                // Try to detect Steam library folders from registry
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    {
                        var steamPath = key?.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(steamPath))
                        {
                            var nwnPath = System.IO.Path.Combine(steamPath, "steamapps", "common", "Neverwinter Nights");
                            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found Steam path from registry: {steamPath}");
                            possiblePaths.Insert(0, nwnPath); // Check registry path first
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not read Steam registry: {ex.Message}");
                }

                // Try GOG paths
                var gogPaths = new[]
                {
                    @"C:\Program Files (x86)\GOG Galaxy\Games\Neverwinter Nights Enhanced Edition",
                    @"C:\GOG Games\Neverwinter Nights Enhanced Edition"
                };
                possiblePaths.AddRange(gogPaths);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                possiblePaths.Add("/Applications/Neverwinter Nights.app/Contents/Resources");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                possiblePaths.Add(System.IO.Path.Combine(home, ".steam", "steam", "steamapps", "common", "Neverwinter Nights"));
            }

            string? detectedPath = null;
            foreach (var path in possiblePaths)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Checking: {path}");
                if (System.IO.Directory.Exists(path))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  Directory exists, checking for data\\ folder...");
                    var dataPath = System.IO.Path.Combine(path, "data");
                    if (System.IO.Directory.Exists(dataPath))
                    {
                        detectedPath = path;
                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-detected base game path: {UnifiedLogger.SanitizePath(detectedPath)}");
                        break;
                    }
                    else
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  Missing data\\ folder");
                    }
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  Directory does not exist");
                }
            }

            var baseGamePathTextBox = this.FindControl<TextBox>("BaseGamePathTextBox");
            if (!string.IsNullOrEmpty(detectedPath) && baseGamePathTextBox != null)
            {
                baseGamePathTextBox.Text = detectedPath;
                ValidateBaseGamePath(detectedPath);
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "Could not auto-detect base game installation");
                var validation = this.FindControl<TextBlock>("BaseGamePathValidation");
                if (validation != null)
                {
                    validation.Text = "❌ Could not auto-detect base game installation. Please browse manually.";
                    validation.Foreground = Brushes.Red;
                }
            }
        }

        private void ValidateBaseGamePath(string path)
        {
            var validation = this.FindControl<TextBlock>("BaseGamePathValidation");
            if (validation == null) return;

            // Base game installation should have data\ folder
            var dataPath = System.IO.Path.Combine(path, "data");
            if (System.IO.Directory.Exists(dataPath))
            {
                validation.Text = "✅ Valid base game installation (contains data\\ folder)";
                validation.Foreground = Brushes.Green;
            }
            else if (!string.IsNullOrEmpty(path))
            {
                validation.Text = "❌ Invalid path - missing data\\ folder";
                validation.Foreground = Brushes.Red;
            }
            else
            {
                validation.Text = "";
            }
        }

        private async void OnBrowseModulePathClick(object? sender, RoutedEventArgs e)
        {
            var storageProvider = StorageProvider;
            if (storageProvider == null) return;

            // Default to Documents\Neverwinter Nights\modules
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var defaultPath = System.IO.Path.Combine(documentsPath, "Neverwinter Nights", "modules");

            var options = new FolderPickerOpenOptions
            {
                Title = "Select Module Directory (Documents\\Neverwinter Nights\\modules)",
                AllowMultiple = false,
                SuggestedStartLocation = System.IO.Directory.Exists(defaultPath)
                    ? await storageProvider.TryGetFolderFromPathAsync(new Uri(defaultPath))
                    : await storageProvider.TryGetFolderFromPathAsync(new Uri(System.IO.Path.Combine(documentsPath, "Neverwinter Nights")))
            };

            var result = await storageProvider.OpenFolderPickerAsync(options);
            if (result.Count > 0)
            {
                var path = result[0].Path.LocalPath;
                var modulePathTextBox = this.FindControl<TextBox>("ModulePathTextBox");
                if (modulePathTextBox != null)
                {
                    modulePathTextBox.Text = path;
                    ValidateModulePath(path);
                }
            }
        }

        private void OnAutoDetectModulePathClick(object? sender, RoutedEventArgs e)
        {
            var gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox");
            var gamePath = gamePathTextBox?.Text;

            var detectedPath = ResourcePathHelper.AutoDetectModulePath(gamePath);
            var modulePathTextBox = this.FindControl<TextBox>("ModulePathTextBox");

            if (!string.IsNullOrEmpty(detectedPath) && modulePathTextBox != null)
            {
                modulePathTextBox.Text = detectedPath;
                ValidateModulePath(detectedPath);
            }
            else
            {
                var validation = this.FindControl<TextBlock>("ModulePathValidation");
                if (validation != null)
                {
                    validation.Text = "❌ Could not auto-detect module path. Please browse manually.";
                    validation.Foreground = Brushes.Red;
                }
            }
        }

        private void ValidateGamePath(string path)
        {
            var validation = this.FindControl<TextBlock>("GamePathValidation");
            if (validation == null) return;

            if (ResourcePathHelper.ValidateGamePath(path))
            {
                validation.Text = "✅ Valid game installation path";
                validation.Foreground = Brushes.Green;
            }
            else if (!string.IsNullOrEmpty(path))
            {
                validation.Text = "❌ Invalid path - missing required directories (ambient, music)";
                validation.Foreground = Brushes.Red;
            }
            else
            {
                validation.Text = "";
            }
        }

        private void ValidateModulePath(string path)
        {
            var validation = this.FindControl<TextBlock>("ModulePathValidation");
            if (validation == null) return;

            if (ResourcePathHelper.ValidateModulePath(path))
            {
                validation.Text = "✅ Valid module directory";
                validation.Foreground = Brushes.Green;
            }
            else if (!string.IsNullOrEmpty(path))
            {
                validation.Text = "❌ Invalid path - no .mod files or module directories found";
                validation.Foreground = Brushes.Red;
            }
            else
            {
                validation.Text = "";
            }
        }

        private void OnRecentModuleSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var listBox = sender as ListBox;
            if (listBox?.SelectedItem is string selectedPath)
            {
                var modulePathTextBox = this.FindControl<TextBox>("ModulePathTextBox");
                if (modulePathTextBox != null)
                {
                    modulePathTextBox.Text = selectedPath;
                    ValidateModulePath(selectedPath);
                }
            }
        }

        private void OnClearRecentModulesClick(object? sender, RoutedEventArgs e)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Clear Recent Modules clicked");
            SettingsService.Instance.ClearModulePaths();

            // Refresh the list box display
            var recentModulesListBox = this.FindControl<ListBox>("RecentModulesListBox");
            if (recentModulesListBox != null)
            {
                recentModulesListBox.ItemsSource = null;
                recentModulesListBox.ItemsSource = SettingsService.Instance.ModulePaths;
            }
        }

        private void OnThemeChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            // Determine which radio button was clicked
            var senderRadio = sender as RadioButton;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"OnThemeChanged: sender={senderRadio?.Name}, IsChecked={senderRadio?.IsChecked}");

            // Apply theme based on which button was checked
            if (senderRadio?.IsChecked == true)
            {
                bool isDark = senderRadio.Name == "DarkThemeRadio";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Theme applied: {(isDark ? "Dark" : "Light")} (from {senderRadio.Name})");

                if (Application.Current != null)
                {
                    Application.Current.RequestedThemeVariant = isDark
                        ? global::Avalonia.Styling.ThemeVariant.Dark
                        : global::Avalonia.Styling.ThemeVariant.Light;
                }
            }
        }

        private void OnFontSizeChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            var fontSizeLabel = this.FindControl<TextBlock>("FontSizeLabel");
            if (fontSizeLabel != null && sender is Slider slider)
            {
                fontSizeLabel.Text = slider.Value.ToString("0");
            }

            // Apply font size immediately when slider changes
            if (!_isInitializing)
            {
                ApplyFontSizePreview();
                UpdateFontPreview();
            }
        }

        private void OnLogLevelChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            // Log level change will be applied when OK or Apply is clicked
        }

        private void OnLogRetentionChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            var logRetentionLabel = this.FindControl<TextBlock>("LogRetentionLabel");
            if (logRetentionLabel != null && sender is Slider slider)
            {
                logRetentionLabel.Text = $"{(int)slider.Value} sessions";
            }
        }

        private void OnSafeModeChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            var safeModeCheckBox = this.FindControl<CheckBox>("SafeModeCheckBox");
            if (safeModeCheckBox != null)
            {
                PluginSettingsService.Instance.SafeMode = safeModeCheckBox.IsChecked == true;
            }
        }

        private void OnOpenPluginsFolderClick(object? sender, RoutedEventArgs e)
        {
            var userDataDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Parley",
                "Plugins",
                "Community"
            );

            // Create directory if it doesn't exist
            if (!System.IO.Directory.Exists(userDataDir))
            {
                System.IO.Directory.CreateDirectory(userDataDir);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Created plugins directory: {userDataDir}");
            }

            // Open in file explorer
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

        private void OnRefreshPluginsClick(object? sender, RoutedEventArgs e)
        {
            LoadPluginList();
            UnifiedLogger.LogApplication(LogLevel.INFO, "Plugin list refreshed");
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            ApplySettings();
            Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnApplyClick(object? sender, RoutedEventArgs e)
        {
            ApplySettings();
        }

        private void ApplySettings()
        {
            var settings = SettingsService.Instance;

            // Resource Paths
            var gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox");
            var baseGamePathTextBox = this.FindControl<TextBox>("BaseGamePathTextBox"); // Phase 2
            var modulePathTextBox = this.FindControl<TextBox>("ModulePathTextBox");

            if (gamePathTextBox != null)
            {
                var gamePath = gamePathTextBox.Text ?? "";
                if (ResourcePathHelper.ValidateGamePath(gamePath) || string.IsNullOrEmpty(gamePath))
                {
                    settings.NeverwinterNightsPath = gamePath;
                }
            }

            // Phase 2: Save base game installation path
            if (baseGamePathTextBox != null)
            {
                var baseGamePath = baseGamePathTextBox.Text ?? "";
                // Validate: should have data\ folder, but allow empty
                var dataPath = string.IsNullOrEmpty(baseGamePath) ? "" : System.IO.Path.Combine(baseGamePath, "data");
                if (string.IsNullOrEmpty(baseGamePath) || System.IO.Directory.Exists(dataPath))
                {
                    settings.BaseGameInstallPath = baseGamePath;
                }
            }

            if (modulePathTextBox != null)
            {
                var modulePath = modulePathTextBox.Text ?? "";
                if (ResourcePathHelper.ValidateModulePath(modulePath) || string.IsNullOrEmpty(modulePath))
                {
                    settings.CurrentModulePath = modulePath;

                    // Add to recent modules if valid and not empty
                    if (!string.IsNullOrEmpty(modulePath) && ResourcePathHelper.ValidateModulePath(modulePath))
                    {
                        settings.AddModulePath(modulePath);
                    }
                }
            }

            // UI Settings
            var darkThemeRadio = this.FindControl<RadioButton>("DarkThemeRadio");
            var fontSizeSlider = this.FindControl<Slider>("FontSizeSlider");
            var externalEditorPathTextBox = this.FindControl<TextBox>("ExternalEditorPathTextBox");

            if (darkThemeRadio != null)
            {
                settings.IsDarkTheme = darkThemeRadio.IsChecked == true;
            }

            if (fontSizeSlider != null)
            {
                settings.FontSize = fontSizeSlider.Value;
            }

            // Save font family
            var fontFamilyComboBox = this.FindControl<ComboBox>("FontFamilyComboBox");
            if (fontFamilyComboBox?.SelectedItem is string selectedFont)
            {
                settings.FontFamily = selectedFont == "System Default" ? "" : selectedFont;
            }

            if (externalEditorPathTextBox != null)
            {
                settings.ExternalEditorPath = externalEditorPathTextBox.Text ?? "";
            }

            // Logging Settings
            var logLevelComboBox = this.FindControl<ComboBox>("LogLevelComboBox");
            var logRetentionSlider = this.FindControl<Slider>("LogRetentionSlider");

            if (logLevelComboBox?.SelectedItem is LogLevel logLevel)
            {
                settings.CurrentLogLevel = logLevel;
            }

            if (logRetentionSlider != null)
            {
                settings.LogRetentionSessions = (int)logRetentionSlider.Value;
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, "Settings applied successfully");
        }

        private void ApplyThemePreview()
        {
            try
            {
                var lightThemeRadio = this.FindControl<RadioButton>("LightThemeRadio");
                var darkThemeRadio = this.FindControl<RadioButton>("DarkThemeRadio");

                bool isDark = darkThemeRadio?.IsChecked == true;
                bool isLight = lightThemeRadio?.IsChecked == true;

                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"ApplyThemePreview: Light={isLight}, Dark={isDark}");

                if (Application.Current != null)
                {
                    Application.Current.RequestedThemeVariant = isDark ? global::Avalonia.Styling.ThemeVariant.Dark : global::Avalonia.Styling.ThemeVariant.Light;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Theme applied: {(isDark ? "Dark" : "Light")}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error applying theme preview: {ex.Message}");
            }
        }

        private void ApplyFontSizePreview()
        {
            try
            {
                var fontSizeSlider = this.FindControl<Slider>("FontSizeSlider");
                if (fontSizeSlider != null)
                {
                    // Fixed in #58 - Apply font size globally using App.ApplyFontSize
                    App.ApplyFontSize(fontSizeSlider.Value);
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Font size preview: {fontSizeSlider.Value}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error applying font size preview: {ex.Message}");
            }
        }

        private void LoadFontFamilies(string currentFontFamily)
        {
            try
            {
                var fontFamilyComboBox = this.FindControl<ComboBox>("FontFamilyComboBox");
                if (fontFamilyComboBox == null) return;

                // Get available system fonts
                var fonts = new ObservableCollection<string> { "System Default" };

                // Add common cross-platform fonts
                var commonFonts = new[]
                {
                    "Arial", "Calibri", "Cambria", "Consolas", "Courier New",
                    "Georgia", "Helvetica", "Segoe UI", "Tahoma", "Times New Roman",
                    "Trebuchet MS", "Verdana",
                    // Platform-specific fonts that may be available
                    "San Francisco", "Ubuntu", "Noto Sans", "Roboto"
                };

                foreach (var font in commonFonts)
                {
                    try
                    {
                        // Test if font exists
                        var testFamily = new FontFamily(font);
                        fonts.Add(font);
                    }
                    catch
                    {
                        // Font not available on this system
                    }
                }

                fontFamilyComboBox.ItemsSource = fonts;

                // Select current font
                if (string.IsNullOrWhiteSpace(currentFontFamily))
                {
                    fontFamilyComboBox.SelectedIndex = 0; // System Default
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

        private void OnFontFamilyChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var fontFamilyComboBox = sender as ComboBox;
            if (fontFamilyComboBox?.SelectedItem is string selectedFont)
            {
                // Apply font family immediately
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

        private void UpdateFontPreview()
        {
            try
            {
                var fontPreviewText = this.FindControl<TextBlock>("FontPreviewText");
                var fontFamilyComboBox = this.FindControl<ComboBox>("FontFamilyComboBox");
                var fontSizeSlider = this.FindControl<Slider>("FontSizeSlider");

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
                            catch
                            {
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

        // Parameter Cache Event Handlers

        private void OnParameterCacheSettingChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            var checkbox = sender as CheckBox;
            if (checkbox != null)
            {
                SettingsService.Instance.EnableParameterCache = checkbox.IsChecked ?? true;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Parameter cache {(checkbox.IsChecked == true ? "enabled" : "disabled")}");
            }
        }

        private void OnMaxCachedValuesChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isInitializing) return;

            var slider = sender as Slider;
            var label = this.FindControl<TextBlock>("MaxCachedValuesLabel");

            if (slider != null && label != null)
            {
                int value = (int)slider.Value;
                label.Text = $"{value} values";
                SettingsService.Instance.MaxCachedValuesPerParameter = value;
            }
        }

        private void OnMaxCachedScriptsChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isInitializing) return;

            var slider = sender as Slider;
            var label = this.FindControl<TextBlock>("MaxCachedScriptsLabel");

            if (slider != null && label != null)
            {
                int value = (int)slider.Value;
                label.Text = $"{value} scripts";
                SettingsService.Instance.MaxCachedScripts = value;
            }
        }

        private void UpdateCacheStats()
        {
            try
            {
                var stats = ParameterCacheService.Instance.GetStats();
                var statsText = this.FindControl<TextBlock>("CacheStatsText");

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

        private async void OnClearParameterCacheClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Show confirmation (non-modal)
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

        private void OnRefreshCacheStatsClick(object? sender, RoutedEventArgs e)
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
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
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
            await dialog.ShowDialog(this);

            return result;
        }
    }
}
