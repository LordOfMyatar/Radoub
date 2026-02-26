using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;
using System;
using System.Diagnostics;
using System.IO;

namespace Manifest.Views;

/// <summary>
/// SettingsWindow partial: Game path, user path, and module configuration settings.
/// </summary>
public partial class SettingsWindow
{
    #region Game Path

    private void LoadGamePath()
    {
        var radoubSettings = RadoubSettings.Instance;
        GamePathTextBox.Text = radoubSettings.BaseGameInstallPath;
        UpdateGamePathValidation();

        UserPathTextBox.Text = radoubSettings.NeverwinterNightsPath;
        UpdateUserPathValidation();
    }

    private void OnGamePathTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;

        var rawPath = GamePathTextBox.Text ?? "";
        var path = string.IsNullOrWhiteSpace(rawPath) ? "" : Path.GetFullPath(rawPath);
        RadoubSettings.Instance.BaseGameInstallPath = path;
        UpdateGamePathValidation();
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Game path changed to: {SanitizePath(path)}");
    }

    private async void OnBrowseGamePath(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var options = new FolderPickerOpenOptions
        {
            Title = "Select Neverwinter Nights Installation Folder",
            AllowMultiple = false
        };

        var result = await storageProvider.OpenFolderPickerAsync(options);
        if (result.Count > 0)
        {
            var path = Path.GetFullPath(result[0].Path.LocalPath);
            GamePathTextBox.Text = path;
            RadoubSettings.Instance.BaseGameInstallPath = path;
            UpdateGamePathValidation();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Game path set via browse: {SanitizePath(path)}");
        }
    }

    private void OnAutoDetectGamePath(object? sender, RoutedEventArgs e)
    {
        var detectedPath = ResourcePathDetector.AutoDetectBaseGamePath();

        if (!string.IsNullOrEmpty(detectedPath))
        {
            GamePathTextBox.Text = detectedPath;
            RadoubSettings.Instance.BaseGameInstallPath = detectedPath;
            UpdateGamePathValidation();
            GamePathValidationText.Text = StatusIndicatorHelper.FormatValidation("Auto-detected game installation", true);
            GamePathValidationText.Foreground = GetSuccessBrush();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Game path auto-detected: {SanitizePath(detectedPath)}");
        }
        else
        {
            GamePathValidationText.Text = StatusIndicatorHelper.FormatWarning("Could not auto-detect game path. Please browse manually.");
            GamePathValidationText.Foreground = GetWarningBrush();
            UnifiedLogger.LogApplication(LogLevel.WARN, "Game path auto-detection failed");
        }
    }

    private void UpdateGamePathValidation()
    {
        var path = GamePathTextBox.Text ?? "";

        if (string.IsNullOrEmpty(path))
        {
            GamePathValidationText.Text = "";
            return;
        }

        var result = ResourcePathDetector.ValidateBaseGamePathWithMessage(path);
        GamePathValidationText.Text = StatusIndicatorHelper.FormatValidation(result.Message, result.IsValid);
        GamePathValidationText.Foreground = result.IsValid
            ? GetSuccessBrush()
            : GetErrorBrush();
    }

    private static string SanitizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile) && path.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
        {
            return "~" + path.Substring(userProfile.Length);
        }
        return path;
    }

    #endregion

    #region User Path

    private void OnUserPathTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;

        var rawPath = UserPathTextBox.Text ?? "";
        var path = string.IsNullOrWhiteSpace(rawPath) ? "" : Path.GetFullPath(rawPath);
        RadoubSettings.Instance.NeverwinterNightsPath = path;
        UpdateUserPathValidation();
        UnifiedLogger.LogApplication(LogLevel.INFO, $"User path changed to: {SanitizePath(path)}");
    }

    private async void OnBrowseUserPath(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var options = new FolderPickerOpenOptions
        {
            Title = "Select Neverwinter Nights User Folder",
            AllowMultiple = false
        };

        var result = await storageProvider.OpenFolderPickerAsync(options);
        if (result.Count > 0)
        {
            var path = Path.GetFullPath(result[0].Path.LocalPath);
            UserPathTextBox.Text = path;
            RadoubSettings.Instance.NeverwinterNightsPath = path;
            UpdateUserPathValidation();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"User path set via browse: {SanitizePath(path)}");
        }
    }

    private void OnAutoDetectUserPath(object? sender, RoutedEventArgs e)
    {
        var detectedPath = ResourcePathDetector.AutoDetectGamePath();

        if (!string.IsNullOrEmpty(detectedPath))
        {
            UserPathTextBox.Text = detectedPath;
            RadoubSettings.Instance.NeverwinterNightsPath = detectedPath;
            UpdateUserPathValidation();
            UserPathValidationText.Text = StatusIndicatorHelper.FormatValidation("Auto-detected user documents", true);
            UserPathValidationText.Foreground = GetSuccessBrush();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"User path auto-detected: {SanitizePath(detectedPath)}");
        }
        else
        {
            UserPathValidationText.Text = StatusIndicatorHelper.FormatWarning("Could not auto-detect user path. Please browse manually.");
            UserPathValidationText.Foreground = GetWarningBrush();
            UnifiedLogger.LogApplication(LogLevel.WARN, "User path auto-detection failed");
        }
    }

    private void UpdateUserPathValidation()
    {
        var path = UserPathTextBox.Text ?? "";

        if (string.IsNullOrEmpty(path))
        {
            UserPathValidationText.Text = "";
            return;
        }

        var result = ResourcePathDetector.ValidateGamePathWithMessage(path);
        UserPathValidationText.Text = StatusIndicatorHelper.FormatValidation(result.Message, result.IsValid);
        UserPathValidationText.Foreground = result.IsValid
            ? GetSuccessBrush()
            : GetErrorBrush();
    }

    #endregion

    #region Module Configuration (#1325, #1322)

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
        var trebuchetPath = RadoubSettings.Instance.TrebuchetPath;

        if (string.IsNullOrEmpty(trebuchetPath) || !File.Exists(trebuchetPath))
        {
            TrebuchetStatusText.Text = "Trebuchet not found. Launch Trebuchet once to register its path.";
            TrebuchetStatusText.Foreground = BrushManager.GetWarningBrush(this);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = trebuchetPath,
                UseShellExecute = true
            });

            TrebuchetStatusText.Text = "Trebuchet launched. Restart this tool after changing module.";
            TrebuchetStatusText.Foreground = BrushManager.GetInfoBrush(this);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to launch Trebuchet: {ex.Message}");
            TrebuchetStatusText.Text = $"Failed to launch Trebuchet: {ex.Message}";
            TrebuchetStatusText.Foreground = BrushManager.GetErrorBrush(this);
        }
    }

    #endregion
}
