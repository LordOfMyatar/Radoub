using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ItemEditor.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;
using System;
using System.IO;

namespace ItemEditor.Views;

public partial class SettingsWindow : Window
{
    private bool _isInitializing = true;

    public SettingsWindow()
    {
        InitializeComponent();

        LoadSettings();

        _isInitializing = false;
    }

    private void LoadSettings()
    {
        LoadModuleConfiguration();
        LoadResourcePaths();
        LoadThemeInfo();
        LoadReliqueOptions();
    }

    #region Module Configuration

    private void LoadModuleConfiguration()
    {
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (RadoubSettings.IsValidModulePath(modulePath))
        {
            string? workingDir = modulePath;
            if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
            {
                var moduleName = Path.GetFileNameWithoutExtension(modulePath);
                var moduleDir = Path.GetDirectoryName(modulePath);
                if (!string.IsNullOrEmpty(moduleDir))
                {
                    var candidate = Path.Combine(moduleDir, moduleName);
                    if (Directory.Exists(candidate))
                        workingDir = candidate;
                }
            }

            string? displayName = null;
            if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
            {
                var ifoPath = Path.Combine(workingDir, "module.ifo");
                if (File.Exists(ifoPath))
                {
                    try
                    {
                        var ifo = Radoub.Formats.Ifo.IfoReader.Read(ifoPath);
                        displayName = ifo.ModuleName.GetDefault();
                    }
                    catch { /* fall through to path-based name */ }
                }
            }

            CurrentModuleText.Text = displayName ?? Path.GetFileName(modulePath);
            CurrentModuleText.Foreground = BrushManager.GetInfoBrush(this);
        }
        else
        {
            CurrentModuleText.Text = "No module selected";
            CurrentModuleText.Foreground = BrushManager.GetWarningBrush(this);
        }
    }

    private void OnConfigureInTrebuchetClick(object? sender, RoutedEventArgs e)
    {
        LaunchTrebuchetSettings("Trebuchet launched. Restart Relique after changing module.");
    }

    #endregion

    #region Resource Paths

    private void LoadResourcePaths()
    {
        var sharedSettings = RadoubSettings.Instance;
        BaseGamePathTextBox.Text = sharedSettings.BaseGameInstallPath;
        GamePathTextBox.Text = sharedSettings.NeverwinterNightsPath;

        ValidateBaseGamePath();
        ValidateGamePath();
    }

    private async void OnBrowseBaseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NWN Game Installation Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            BaseGamePathTextBox.Text = folders[0].Path.LocalPath;
            RadoubSettings.Instance.BaseGameInstallPath = folders[0].Path.LocalPath;
            ValidateBaseGamePath();
        }
    }

    private void OnAutoDetectBaseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var detected = ResourcePathDetector.AutoDetectBaseGamePath();
        if (!string.IsNullOrEmpty(detected))
        {
            BaseGamePathTextBox.Text = detected;
            RadoubSettings.Instance.BaseGameInstallPath = detected;
            ValidateBaseGamePath();
        }
        else
        {
            BaseGamePathValidation.Text = "Could not auto-detect game path";
            BaseGamePathValidation.Foreground = GetWarningBrush();
        }
    }

    private async void OnBrowseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NWN User Data Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            GamePathTextBox.Text = folders[0].Path.LocalPath;
            RadoubSettings.Instance.NeverwinterNightsPath = folders[0].Path.LocalPath;
            ValidateGamePath();
        }
    }

    private void OnAutoDetectGamePathClick(object? sender, RoutedEventArgs e)
    {
        var detected = ResourcePathDetector.AutoDetectGamePath();
        if (!string.IsNullOrEmpty(detected))
        {
            GamePathTextBox.Text = detected;
            RadoubSettings.Instance.NeverwinterNightsPath = detected;
            ValidateGamePath();
        }
        else
        {
            GamePathValidation.Text = "Could not auto-detect user data path";
            GamePathValidation.Foreground = GetWarningBrush();
        }
    }

    private void ValidateBaseGamePath()
    {
        var path = BaseGamePathTextBox.Text;
        if (string.IsNullOrEmpty(path))
        {
            BaseGamePathValidation.Text = "";
            return;
        }

        if (Directory.Exists(path))
        {
            var dataPath = Path.Combine(path, "data");
            var keyFile = Path.Combine(path, "data", "nwn_base.key");

            if (Directory.Exists(dataPath) || File.Exists(keyFile))
            {
                BaseGamePathValidation.Text = "\u2713 Valid NWN installation detected";
                BaseGamePathValidation.Foreground = GetSuccessBrush();
            }
            else
            {
                BaseGamePathValidation.Text = "\u26A0 Directory exists but may not be NWN installation";
                BaseGamePathValidation.Foreground = GetWarningBrush();
            }
        }
        else
        {
            BaseGamePathValidation.Text = "\u2717 Directory does not exist";
            BaseGamePathValidation.Foreground = GetErrorBrush();
        }
    }

    private void ValidateGamePath()
    {
        var path = GamePathTextBox.Text;
        if (string.IsNullOrEmpty(path))
        {
            GamePathValidation.Text = "";
            return;
        }

        if (Directory.Exists(path))
        {
            var modulesPath = Path.Combine(path, "modules");
            if (Directory.Exists(modulesPath))
            {
                GamePathValidation.Text = "\u2713 Valid NWN user data directory";
                GamePathValidation.Foreground = GetSuccessBrush();
            }
            else
            {
                GamePathValidation.Text = "\u26A0 Directory exists but no modules folder found";
                GamePathValidation.Foreground = GetWarningBrush();
            }
        }
        else
        {
            GamePathValidation.Text = "\u2717 Directory does not exist";
            GamePathValidation.Foreground = GetErrorBrush();
        }
    }

    #endregion

    #region Theme Info

    private void LoadThemeInfo()
    {
        var settings = RadoubSettings.Instance;

        var themeId = settings.SharedThemeId;
        CurrentThemeText.Text = string.IsNullOrEmpty(themeId) ? "Default" : themeId;

        CurrentFontSizeText.Text = $"{settings.SharedFontSize}pt";
        CurrentFontFamilyText.Text = settings.SharedFontFamily;
    }

    private void OnManageThemeInTrebuchetClick(object? sender, RoutedEventArgs e)
    {
        LaunchTrebuchetSettings("Trebuchet launched. Theme changes apply after restart.");
    }

    #endregion

    #region Relique Options

    private void LoadReliqueOptions()
    {
        OpenInEditorCheckBox.IsChecked = SettingsService.Instance.OpenInEditorAfterCreate;
    }

    private void OnOpenInEditorCheckBoxClick(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.Instance.OpenInEditorAfterCreate = OpenInEditorCheckBox.IsChecked == true;
    }

    #endregion

    #region Helpers

    private void LaunchTrebuchetSettings(string successMessage)
    {
        var trebuchetPath = RadoubSettings.Instance.TrebuchetPath;

        if (string.IsNullOrEmpty(trebuchetPath) || !File.Exists(trebuchetPath))
        {
            TrebuchetStatusText.Text = "Trebuchet not found. Launch Trebuchet once to register its path.";
            TrebuchetStatusText.Foreground = BrushManager.GetWarningBrush(this);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = trebuchetPath,
                Arguments = "--settings",
                UseShellExecute = false
            });

            TrebuchetStatusText.Text = successMessage;
            TrebuchetStatusText.Foreground = BrushManager.GetInfoBrush(this);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to launch Trebuchet: {ex.Message}");
            TrebuchetStatusText.Text = $"Failed to launch Trebuchet: {ex.Message}";
            TrebuchetStatusText.Foreground = BrushManager.GetErrorBrush(this);
        }
    }

    private IBrush GetSuccessBrush() => BrushManager.GetSuccessBrush(this);
    private IBrush GetWarningBrush() => BrushManager.GetWarningBrush(this);
    private IBrush GetErrorBrush() => BrushManager.GetErrorBrush(this);

    #endregion

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
