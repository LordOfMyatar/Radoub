using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace DialogEditor.Views.Controllers
{
    /// <summary>
    /// Controller for Resource Paths settings section in SettingsWindow.
    /// Handles: Game path, base game path, module path, recent modules, TLK language settings.
    /// </summary>
    public class ResourcePathsController
    {
        private readonly Window _window;
        private readonly Func<bool> _isInitializing;
        private readonly Func<IBrush> _getErrorBrush;
        private readonly Func<IBrush> _getSuccessBrush;

        public ResourcePathsController(
            Window window,
            Func<bool> isInitializing,
            Func<IBrush> getErrorBrush,
            Func<IBrush> getSuccessBrush)
        {
            _window = window;
            _isInitializing = isInitializing;
            _getErrorBrush = getErrorBrush;
            _getSuccessBrush = getSuccessBrush;
        }

        public void LoadSettings()
        {
            var settings = SettingsService.Instance;

            // Game Path (User Data Directory)
            var gamePathTextBox = _window.FindControl<TextBox>("GamePathTextBox");
            if (gamePathTextBox != null)
            {
                gamePathTextBox.Text = settings.NeverwinterNightsPath;
                if (!string.IsNullOrEmpty(settings.NeverwinterNightsPath))
                {
                    ValidateGamePath(settings.NeverwinterNightsPath);
                }
            }

            // Base Game Installation Path
            var baseGamePathTextBox = _window.FindControl<TextBox>("BaseGamePathTextBox");
            if (baseGamePathTextBox != null)
            {
                baseGamePathTextBox.Text = settings.BaseGameInstallPath;
                if (!string.IsNullOrEmpty(settings.BaseGameInstallPath))
                {
                    ValidateBaseGamePath(settings.BaseGameInstallPath);
                }
            }

            // Module Path
            var modulePathTextBox = _window.FindControl<TextBox>("ModulePathTextBox");
            if (modulePathTextBox != null)
            {
                modulePathTextBox.Text = settings.CurrentModulePath;
                if (!string.IsNullOrEmpty(settings.CurrentModulePath))
                {
                    ValidateModulePath(settings.CurrentModulePath);
                }
            }

            // Recent Modules
            var recentModulesListBox = _window.FindControl<ListBox>("RecentModulesListBox");
            if (recentModulesListBox != null)
            {
                recentModulesListBox.ItemsSource = settings.ModulePaths;
            }

            // Platform-specific paths info
            var platformPathsInfo = _window.FindControl<TextBlock>("PlatformPathsInfo");
            if (platformPathsInfo != null)
            {
                platformPathsInfo.Text = GetPlatformPathsInfo();
            }

            // TLK Language Settings
            LoadTlkLanguageSettings();
        }

        public void ApplySettings()
        {
            var settings = SettingsService.Instance;

            var gamePathTextBox = _window.FindControl<TextBox>("GamePathTextBox");
            var baseGamePathTextBox = _window.FindControl<TextBox>("BaseGamePathTextBox");
            var modulePathTextBox = _window.FindControl<TextBox>("ModulePathTextBox");

            if (gamePathTextBox != null)
            {
                var gamePath = gamePathTextBox.Text ?? "";
                if (ResourcePathDetector.ValidateGamePath(gamePath) || string.IsNullOrEmpty(gamePath))
                {
                    settings.NeverwinterNightsPath = gamePath;
                }
            }

            if (baseGamePathTextBox != null)
            {
                var baseGamePath = baseGamePathTextBox.Text ?? "";
                var dataPath = string.IsNullOrEmpty(baseGamePath) ? "" : System.IO.Path.Combine(baseGamePath, "data");
                if (string.IsNullOrEmpty(baseGamePath) || System.IO.Directory.Exists(dataPath))
                {
                    settings.BaseGameInstallPath = baseGamePath;
                }
            }

            if (modulePathTextBox != null)
            {
                var modulePath = modulePathTextBox.Text ?? "";
                if (ResourcePathDetector.ValidateModulePath(modulePath) || string.IsNullOrEmpty(modulePath))
                {
                    settings.CurrentModulePath = modulePath;

                    if (!string.IsNullOrEmpty(modulePath) && ResourcePathDetector.ValidateModulePath(modulePath))
                    {
                        settings.AddModulePath(modulePath);
                    }
                }
            }
        }

        #region TLK Language Settings

        private void LoadTlkLanguageSettings()
        {
            var settings = SettingsService.Instance;
            var tlkLanguageComboBox = _window.FindControl<ComboBox>("TlkLanguageComboBox");
            var tlkUseFemaleCheckBox = _window.FindControl<CheckBox>("TlkUseFemaleCheckBox");

            if (tlkLanguageComboBox != null)
            {
                var currentLang = settings.TlkLanguage ?? "";
                foreach (var item in tlkLanguageComboBox.Items)
                {
                    if (item is ComboBoxItem comboItem && (comboItem.Tag as string) == currentLang)
                    {
                        tlkLanguageComboBox.SelectedItem = comboItem;
                        break;
                    }
                }
            }

            if (tlkUseFemaleCheckBox != null)
            {
                tlkUseFemaleCheckBox.IsChecked = settings.TlkUseFemale;
            }

            UpdateTlkLanguageStatus();
        }

        public void UpdateTlkLanguageStatus()
        {
            var tlkLanguageStatus = _window.FindControl<TextBlock>("TlkLanguageStatus");
            if (tlkLanguageStatus == null) return;

            var settings = SettingsService.Instance;

            if (string.IsNullOrEmpty(settings.BaseGameInstallPath))
            {
                tlkLanguageStatus.Text = "⚠️ Base game installation path not configured. TLK files cannot be loaded.";
                return;
            }

            var langCode = settings.TlkLanguage;
            if (string.IsNullOrEmpty(langCode))
            {
                langCode = "en";
            }

            var tlkFileName = settings.TlkUseFemale ? "dialogf.tlk" : "dialog.tlk";
            var tlkPath = System.IO.Path.Combine(settings.BaseGameInstallPath, "lang", langCode, "data", tlkFileName);

            if (System.IO.File.Exists(tlkPath))
            {
                tlkLanguageStatus.Text = $"✓ TLK file found: lang/{langCode}/data/{tlkFileName}";
            }
            else
            {
                var langDir = System.IO.Path.Combine(settings.BaseGameInstallPath, "lang", langCode);
                if (!System.IO.Directory.Exists(langDir))
                {
                    tlkLanguageStatus.Text = $"⚠️ Language folder not found: lang/{langCode}/ - verify game installation";
                }
                else
                {
                    tlkLanguageStatus.Text = $"⚠️ TLK file not found: lang/{langCode}/data/{tlkFileName}";
                }
            }
        }

        #endregion

        #region Browse Handlers

        public async void OnBrowseGamePathClick(object? sender, RoutedEventArgs e)
        {
            var validation = _window.FindControl<TextBlock>("GamePathValidation");
            if (validation != null)
            {
                validation.Text = "";
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Browse User Data Directory clicked");

            var storageProvider = _window.StorageProvider;
            if (storageProvider == null) return;

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
                var gamePathTextBox = _window.FindControl<TextBox>("GamePathTextBox");
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

        public void OnAutoDetectGamePathClick(object? sender, RoutedEventArgs e)
        {
            var detectedPath = ResourcePathDetector.AutoDetectGamePath();
            var gamePathTextBox = _window.FindControl<TextBox>("GamePathTextBox");

            if (!string.IsNullOrEmpty(detectedPath) && gamePathTextBox != null)
            {
                gamePathTextBox.Text = detectedPath;
                ValidateGamePath(detectedPath);
            }
            else
            {
                var validation = _window.FindControl<TextBlock>("GamePathValidation");
                if (validation != null)
                {
                    validation.Text = "❌ Could not auto-detect game path. Please browse manually.";
                    validation.Foreground = _getErrorBrush();
                }
            }
        }

        public async void OnBrowseBaseGamePathClick(object? sender, RoutedEventArgs e)
        {
            var validation = _window.FindControl<TextBlock>("BaseGamePathValidation");
            if (validation != null)
            {
                validation.Text = "";
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Browse Base Game Installation clicked");

            var storageProvider = _window.StorageProvider;
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
                var baseGamePathTextBox = _window.FindControl<TextBox>("BaseGamePathTextBox");
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

        public void OnAutoDetectBaseGamePathClick(object? sender, RoutedEventArgs e)
        {
            var detectedPath = ResourcePathDetector.AutoDetectBaseGamePath();
            var baseGamePathTextBox = _window.FindControl<TextBox>("BaseGamePathTextBox");

            if (!string.IsNullOrEmpty(detectedPath) && baseGamePathTextBox != null)
            {
                baseGamePathTextBox.Text = detectedPath;
                ValidateBaseGamePath(detectedPath);
            }
            else
            {
                var validation = _window.FindControl<TextBlock>("BaseGamePathValidation");
                if (validation != null)
                {
                    validation.Text = "❌ Could not auto-detect base game installation. Please browse manually.";
                    validation.Foreground = _getErrorBrush();
                }
            }
        }

        public async void OnBrowseModulePathClick(object? sender, RoutedEventArgs e)
        {
            var storageProvider = _window.StorageProvider;
            if (storageProvider == null) return;

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
                var modulePathTextBox = _window.FindControl<TextBox>("ModulePathTextBox");
                if (modulePathTextBox != null)
                {
                    modulePathTextBox.Text = path;
                    ValidateModulePath(path);
                }
            }
        }

        public void OnAutoDetectModulePathClick(object? sender, RoutedEventArgs e)
        {
            var gamePathTextBox = _window.FindControl<TextBox>("GamePathTextBox");
            var gamePath = gamePathTextBox?.Text;

            var detectedPath = ResourcePathDetector.AutoDetectModulePath(gamePath);
            var modulePathTextBox = _window.FindControl<TextBox>("ModulePathTextBox");

            if (!string.IsNullOrEmpty(detectedPath) && modulePathTextBox != null)
            {
                modulePathTextBox.Text = detectedPath;
                ValidateModulePath(detectedPath);
            }
            else
            {
                var validation = _window.FindControl<TextBlock>("ModulePathValidation");
                if (validation != null)
                {
                    validation.Text = "❌ Could not auto-detect module path. Please browse manually.";
                    validation.Foreground = _getErrorBrush();
                }
            }
        }

        #endregion

        #region Event Handlers

        public void OnRecentModuleSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing()) return;

            var listBox = sender as ListBox;
            if (listBox?.SelectedItem is string selectedPath)
            {
                var modulePathTextBox = _window.FindControl<TextBox>("ModulePathTextBox");
                if (modulePathTextBox != null)
                {
                    modulePathTextBox.Text = selectedPath;
                    ValidateModulePath(selectedPath);
                }
            }
        }

        public void OnClearRecentModulesClick(object? sender, RoutedEventArgs e)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Clear Recent Modules clicked");
            SettingsService.Instance.ClearModulePaths();

            var recentModulesListBox = _window.FindControl<ListBox>("RecentModulesListBox");
            if (recentModulesListBox != null)
            {
                recentModulesListBox.ItemsSource = null;
                recentModulesListBox.ItemsSource = SettingsService.Instance.ModulePaths;
            }
        }

        #endregion

        #region Validation

        public void ValidateGamePath(string path)
        {
            var validation = _window.FindControl<TextBlock>("GamePathValidation");
            if (validation == null) return;

            var result = ResourcePathDetector.ValidateGamePathWithMessage(path);
            validation.Text = StatusIndicatorHelper.FormatValidation(result.Message, result.IsValid);
            validation.Foreground = result.IsValid ? _getSuccessBrush() : _getErrorBrush();
        }

        public void ValidateBaseGamePath(string path)
        {
            var validation = _window.FindControl<TextBlock>("BaseGamePathValidation");
            if (validation == null) return;

            var result = ResourcePathDetector.ValidateBaseGamePathWithMessage(path);
            validation.Text = StatusIndicatorHelper.FormatValidation(result.Message, result.IsValid);
            validation.Foreground = result.IsValid ? _getSuccessBrush() : _getErrorBrush();
        }

        public void ValidateModulePath(string path)
        {
            var validation = _window.FindControl<TextBlock>("ModulePathValidation");
            if (validation == null) return;

            var result = ResourcePathDetector.ValidateModulePathWithMessage(path);
            validation.Text = StatusIndicatorHelper.FormatValidation(result.Message, result.IsValid);
            validation.Foreground = result.IsValid ? _getSuccessBrush() : _getErrorBrush();
        }

        #endregion

        #region Platform Info

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

        #endregion
    }
}
