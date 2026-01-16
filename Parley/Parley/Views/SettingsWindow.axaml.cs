using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using DialogEditor.Services;
using DialogEditor.Views.Controllers;
using Radoub.Formats.Logging;

namespace DialogEditor.Views
{
    public partial class SettingsWindow : Window
    {
        private bool _isInitializing = true;

        // Controllers for section-specific logic
        private ResourcePathsController? _resourcePathsController;
        private ThemeSettingsController? _themeSettingsController;
        private DictionarySettingsController? _dictionarySettingsController;
        private UISettingsController? _uiSettingsController;
        private LoggingSettingsController? _loggingSettingsController;
        private AutoSaveSettingsController? _autoSaveSettingsController;
        private ParameterCacheController? _parameterCacheController;

        // Parameterless constructor for XAML/Avalonia runtime
        public SettingsWindow() : this(0)
        {
        }

        public SettingsWindow(int initialTab = 0)
        {
            InitializeComponent();
            InitializeControllers();
            LoadSettings();
            _isInitializing = false;

            // Apply current theme immediately when dialog opens
            _themeSettingsController?.LoadSettings();

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

        private void InitializeControllers()
        {
            _resourcePathsController = new ResourcePathsController(
                this, () => _isInitializing, GetErrorBrush, GetSuccessBrush);

            _themeSettingsController = new ThemeSettingsController(
                this, () => _isInitializing, GetErrorBrush);

            _dictionarySettingsController = new DictionarySettingsController(
                this, () => _isInitializing);

            _uiSettingsController = new UISettingsController(
                this, () => _isInitializing);

            _loggingSettingsController = new LoggingSettingsController(
                this, () => _isInitializing);

            _autoSaveSettingsController = new AutoSaveSettingsController(
                this, () => _isInitializing);

            _parameterCacheController = new ParameterCacheController(
                this, () => _isInitializing);
        }

        private void LoadSettings()
        {
            // Delegate to controllers
            _resourcePathsController?.LoadSettings();
            _themeSettingsController?.LoadSettings();
            _dictionarySettingsController?.LoadSettings();
            _uiSettingsController?.LoadSettings();
            _loggingSettingsController?.LoadSettings();
            _autoSaveSettingsController?.LoadSettings();
            _parameterCacheController?.LoadSettings();
        }

        #region Resource Paths Handlers (delegated to controller)

        private void OnBrowseGamePathClick(object? sender, RoutedEventArgs e)
            => _resourcePathsController?.OnBrowseGamePathClick(sender, e);

        private void OnAutoDetectGamePathClick(object? sender, RoutedEventArgs e)
            => _resourcePathsController?.OnAutoDetectGamePathClick(sender, e);

        private void OnBrowseBaseGamePathClick(object? sender, RoutedEventArgs e)
            => _resourcePathsController?.OnBrowseBaseGamePathClick(sender, e);

        private void OnAutoDetectBaseGamePathClick(object? sender, RoutedEventArgs e)
            => _resourcePathsController?.OnAutoDetectBaseGamePathClick(sender, e);

        private void OnBrowseModulePathClick(object? sender, RoutedEventArgs e)
            => _resourcePathsController?.OnBrowseModulePathClick(sender, e);

        private void OnAutoDetectModulePathClick(object? sender, RoutedEventArgs e)
            => _resourcePathsController?.OnAutoDetectModulePathClick(sender, e);

        private void OnRecentModuleSelected(object? sender, SelectionChangedEventArgs e)
            => _resourcePathsController?.OnRecentModuleSelected(sender, e);

        private void OnClearRecentModulesClick(object? sender, RoutedEventArgs e)
            => _resourcePathsController?.OnClearRecentModulesClick(sender, e);

        private async void OnTlkLanguageChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var tlkLanguageComboBox = this.FindControl<ComboBox>("TlkLanguageComboBox");
            if (tlkLanguageComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                var langCode = selectedItem.Tag as string ?? "";
                var oldValue = SettingsService.Instance.TlkLanguage;

                if (langCode != oldValue)
                {
                    SettingsService.Instance.TlkLanguage = langCode;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"TLK language changed from '{oldValue}' to '{langCode}'");

                    GameResourceService.Instance.InvalidateResolver();

                    _resourcePathsController?.UpdateTlkLanguageStatus();
                    await PromptReloadDialog();
                }
            }
        }

        private async void OnTlkUseFemaleChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            var tlkUseFemaleCheckBox = this.FindControl<CheckBox>("TlkUseFemaleCheckBox");
            if (tlkUseFemaleCheckBox != null)
            {
                var useFemale = tlkUseFemaleCheckBox.IsChecked ?? false;
                var oldValue = SettingsService.Instance.TlkUseFemale;

                if (useFemale != oldValue)
                {
                    SettingsService.Instance.TlkUseFemale = useFemale;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"TLK female variant changed from {oldValue} to {useFemale}");

                    GameResourceService.Instance.InvalidateResolver();

                    _resourcePathsController?.UpdateTlkLanguageStatus();
                    await PromptReloadDialog();
                }
            }
        }

        private async Task PromptReloadDialog()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow is MainWindow mainWindow && mainWindow.DataContext is ViewModels.MainViewModel vm)
                {
                    if (!string.IsNullOrEmpty(vm.CurrentFilePath))
                    {
                        var result = await ShowReloadConfirmDialog();
                        if (result)
                        {
                            await vm.ReloadCurrentDialogAsync();
                        }
                    }
                }
            }
        }

        private async Task<bool> ShowReloadConfirmDialog()
        {
            var dialog = new Window
            {
                Title = "Reload Dialog?",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            bool result = false;

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = "TLK language settings have changed. Would you like to reload the current dialog to see the changes?",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            });

            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };

            var yesButton = new Button { Content = "Reload", Width = 80 };
            yesButton.Click += (s, e) => { result = true; dialog.Close(); };
            buttonPanel.Children.Add(yesButton);

            var noButton = new Button { Content = "Later", Width = 80 };
            noButton.Click += (s, e) => { result = false; dialog.Close(); };
            buttonPanel.Children.Add(noButton);

            panel.Children.Add(buttonPanel);

            dialog.Content = panel;
            await dialog.ShowDialog(this);

            return result;
        }

        #endregion

        #region Theme Settings Handlers (delegated to controller)

        private void OnThemeComboBoxChanged(object? sender, SelectionChangedEventArgs e)
            => _themeSettingsController?.OnThemeComboBoxChanged(sender, e);

        private void OnGetThemesClick(object? sender, RoutedEventArgs e)
            => _themeSettingsController?.OnGetThemesClick(sender, e);

        private void OnEasterEggHintClick(object? sender, Avalonia.Input.PointerPressedEventArgs e)
            => _themeSettingsController?.OnEasterEggHintClick(sender, e);

        #endregion

        #region Dictionary Settings Handlers (delegated to controller)

        private void OnPrimaryLanguageChanged(object? sender, SelectionChangedEventArgs e)
            => _dictionarySettingsController?.OnPrimaryLanguageChanged(sender, e);

        private void OnOpenDictionariesFolderClick(object? sender, RoutedEventArgs e)
            => _dictionarySettingsController?.OnOpenDictionariesFolderClick(sender, e);

        private void OnRefreshDictionariesClick(object? sender, RoutedEventArgs e)
            => _dictionarySettingsController?.OnRefreshDictionariesClick(sender, e);

        #endregion

        #region UI Settings Handlers (delegated to controller)

        private void OnFontSizeChanged(object? sender, RangeBaseValueChangedEventArgs e)
            => _uiSettingsController?.OnFontSizeChanged(sender, e);

        private void OnFontFamilyChanged(object? sender, SelectionChangedEventArgs e)
            => _uiSettingsController?.OnFontFamilyChanged(sender, e);

        private void OnAllowScrollbarAutoHideChanged(object? sender, RoutedEventArgs e)
            => _uiSettingsController?.OnAllowScrollbarAutoHideChanged(sender, e);

        private void OnFlowchartNodeMaxLinesChanged(object? sender, RangeBaseValueChangedEventArgs e)
            => _uiSettingsController?.OnFlowchartNodeMaxLinesChanged(sender, e);

        private void OnEnableNpcTagColoringChanged(object? sender, RoutedEventArgs e)
            => _uiSettingsController?.OnEnableNpcTagColoringChanged(sender, e);

        private void OnSimulatorShowWarningsChanged(object? sender, RoutedEventArgs e)
            => _uiSettingsController?.OnSimulatorShowWarningsChanged(sender, e);

        private void OnSpellCheckEnabledChanged(object? sender, RoutedEventArgs e)
            => _uiSettingsController?.OnSpellCheckEnabledChanged(sender, e);

        #endregion

        #region External Editor

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

        #endregion

        #region Logging Settings Handlers (delegated to controller)

        private void OnLogLevelChanged(object? sender, SelectionChangedEventArgs e)
            => _loggingSettingsController?.OnLogLevelChanged(sender, e);

        private void OnLogRetentionChanged(object? sender, RangeBaseValueChangedEventArgs e)
            => _loggingSettingsController?.OnLogRetentionChanged(sender, e);

        private void OnShowDebugPanelChanged(object? sender, RoutedEventArgs e)
            => _loggingSettingsController?.OnShowDebugPanelChanged(sender, e);

        #endregion

        #region Auto-Save Settings Handlers (delegated to controller)

        private void OnAutoSaveEnabledChanged(object? sender, RoutedEventArgs e)
            => _autoSaveSettingsController?.OnAutoSaveEnabledChanged(sender, e);

        private void OnAutoSaveIntervalChanged(object? sender, RangeBaseValueChangedEventArgs e)
            => _autoSaveSettingsController?.OnAutoSaveIntervalChanged(sender, e);

        #endregion

        #region Parameter Cache Handlers (delegated to controller)

        private void OnParameterCacheSettingChanged(object? sender, RoutedEventArgs e)
            => _parameterCacheController?.OnParameterCacheSettingChanged(sender, e);

        private void OnMaxCachedValuesChanged(object? sender, RangeBaseValueChangedEventArgs e)
            => _parameterCacheController?.OnMaxCachedValuesChanged(sender, e);

        private void OnMaxCachedScriptsChanged(object? sender, RangeBaseValueChangedEventArgs e)
            => _parameterCacheController?.OnMaxCachedScriptsChanged(sender, e);

        private void OnClearParameterCacheClick(object? sender, RoutedEventArgs e)
            => _parameterCacheController?.OnClearParameterCacheClick(sender, e);

        private void OnRefreshCacheStatsClick(object? sender, RoutedEventArgs e)
            => _parameterCacheController?.OnRefreshCacheStatsClick(sender, e);

        #endregion

        #region Dialog Buttons

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
            // Delegate to controllers
            _resourcePathsController?.ApplySettings();
            _uiSettingsController?.ApplySettings();
            _loggingSettingsController?.ApplySettings();
            _autoSaveSettingsController?.ApplySettings();

            UnifiedLogger.LogApplication(LogLevel.INFO, "Settings applied successfully");
        }

        #endregion

        #region Theme-Aware Colors

        private IBrush GetErrorBrush()
        {
            var app = Application.Current;
            if (app?.Resources.TryGetResource("ThemeError", ThemeVariant.Default, out var errorBrush) == true
                && errorBrush is IBrush brush)
            {
                return brush;
            }
            return Brushes.Red;
        }

        private IBrush GetSuccessBrush()
        {
            var app = Application.Current;
            if (app?.Resources.TryGetResource("ThemeSuccess", ThemeVariant.Default, out var successBrush) == true
                && successBrush is IBrush brush)
            {
                return brush;
            }
            return Brushes.Green;
        }

        #endregion
    }
}
