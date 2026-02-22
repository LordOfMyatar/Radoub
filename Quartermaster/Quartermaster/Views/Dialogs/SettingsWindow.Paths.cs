using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Resource path settings: base game install, user data, browse, validate, auto-detect.
/// </summary>
public partial class SettingsWindow
{
    #region Resource Paths

    private void LoadResourcePathSettings()
    {
        var sharedSettings = RadoubSettings.Instance;

        var baseGamePathTextBox = this.FindControl<TextBox>("BaseGamePathTextBox");
        if (baseGamePathTextBox != null)
        {
            baseGamePathTextBox.Text = sharedSettings.BaseGameInstallPath ?? "";
            ValidateBaseGamePath(sharedSettings.BaseGameInstallPath);
        }

        var gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox");
        if (gamePathTextBox != null)
        {
            gamePathTextBox.Text = sharedSettings.NeverwinterNightsPath ?? "";
            ValidateGamePath(sharedSettings.NeverwinterNightsPath);
        }

        UpdatePlatformPathsInfo();
    }

    private void UpdatePlatformPathsInfo()
    {
        var platformPathsInfo = this.FindControl<TextBlock>("PlatformPathsInfo");
        if (platformPathsInfo == null) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            platformPathsInfo.Text = "Windows:\n" +
                "• Steam: C:\\Program Files (x86)\\Steam\\steamapps\\common\\Neverwinter Nights\n" +
                "• GOG: C:\\GOG Games\\Neverwinter Nights Enhanced Edition\n" +
                "• Beamdog: C:\\Program Files\\Beamdog\\Neverwinter Nights";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            platformPathsInfo.Text = "Linux:\n" +
                "• Steam: ~/.steam/steam/steamapps/common/Neverwinter Nights\n" +
                "• GOG: ~/GOG Games/Neverwinter Nights";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            platformPathsInfo.Text = "macOS:\n" +
                "• Steam: ~/Library/Application Support/Steam/steamapps/common/Neverwinter Nights\n" +
                "• Beamdog: ~/Library/Application Support/Beamdog/Neverwinter Nights";
        }
    }

    private async void OnBrowseBaseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Neverwinter Nights Installation Folder",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            var path = result[0].Path.LocalPath;
            var baseGamePathTextBox = this.FindControl<TextBox>("BaseGamePathTextBox");
            if (baseGamePathTextBox != null)
            {
                baseGamePathTextBox.Text = path;
                ValidateBaseGamePath(path);
            }
        }
    }

    private void OnBaseGamePathTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;

        var textBox = sender as TextBox;
        ValidateBaseGamePath(textBox?.Text);
    }

    private void OnAutoDetectBaseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var detectedPath = DetectBaseGamePath();
        if (!string.IsNullOrEmpty(detectedPath))
        {
            var baseGamePathTextBox = this.FindControl<TextBox>("BaseGamePathTextBox");
            if (baseGamePathTextBox != null)
            {
                baseGamePathTextBox.Text = detectedPath;
                // TextChanged handler will validate
            }
        }
        else
        {
            var validation = this.FindControl<TextBlock>("BaseGamePathValidation");
            if (validation != null)
            {
                validation.Text = StatusIndicatorHelper.FormatWarning("Could not auto-detect installation path");
                validation.Foreground = GetWarningBrush();
            }
        }
    }

    private async void OnBrowseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Neverwinter Nights User Data Folder",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            var path = result[0].Path.LocalPath;
            var gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox");
            if (gamePathTextBox != null)
            {
                gamePathTextBox.Text = path;
                ValidateGamePath(path);
            }
        }
    }

    private void OnGamePathTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;

        var textBox = sender as TextBox;
        ValidateGamePath(textBox?.Text);
    }

    private void OnAutoDetectGamePathClick(object? sender, RoutedEventArgs e)
    {
        var detectedPath = DetectUserDataPath();
        if (!string.IsNullOrEmpty(detectedPath))
        {
            var gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox");
            if (gamePathTextBox != null)
            {
                gamePathTextBox.Text = detectedPath;
                // TextChanged handler will validate
            }
        }
        else
        {
            var validation = this.FindControl<TextBlock>("GamePathValidation");
            if (validation != null)
            {
                validation.Text = StatusIndicatorHelper.FormatWarning("Could not auto-detect user data path");
                validation.Foreground = GetWarningBrush();
            }
        }
    }

    private void ValidateBaseGamePath(string? path)
    {
        var validation = this.FindControl<TextBlock>("BaseGamePathValidation");
        if (validation == null) return;

        if (string.IsNullOrEmpty(path))
        {
            validation.Text = "";
            return;
        }

        if (!Directory.Exists(path))
        {
            validation.Text = StatusIndicatorHelper.FormatValidation("Directory does not exist", false);
            validation.Foreground = GetErrorBrush();
            return;
        }

        var dataPath = Path.Combine(path, "data");
        if (Directory.Exists(dataPath))
        {
            validation.Text = StatusIndicatorHelper.FormatValidation("Valid installation path detected", true);
            validation.Foreground = GetSuccessBrush();
        }
        else
        {
            validation.Text = StatusIndicatorHelper.FormatWarning("'data' folder not found - may not be correct path");
            validation.Foreground = GetWarningBrush();
        }
    }

    private void ValidateGamePath(string? path)
    {
        var validation = this.FindControl<TextBlock>("GamePathValidation");
        if (validation == null) return;

        if (string.IsNullOrEmpty(path))
        {
            validation.Text = "";
            return;
        }

        if (!Directory.Exists(path))
        {
            validation.Text = StatusIndicatorHelper.FormatValidation("Directory does not exist", false);
            validation.Foreground = GetErrorBrush();
            return;
        }

        var modulesPath = Path.Combine(path, "modules");
        if (Directory.Exists(modulesPath))
        {
            validation.Text = StatusIndicatorHelper.FormatValidation("Valid user data path detected", true);
            validation.Foreground = GetSuccessBrush();
        }
        else
        {
            validation.Text = StatusIndicatorHelper.FormatWarning("'modules' folder not found - may not be correct path");
            validation.Foreground = GetWarningBrush();
        }
    }

    private string? DetectBaseGamePath()
    {
        var possiblePaths = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Try Steam registry first
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                var steamPath = key?.GetValue("SteamPath") as string;
                if (!string.IsNullOrEmpty(steamPath))
                {
                    var nwnPath = Path.Combine(steamPath, "steamapps", "common", "Neverwinter Nights");
                    possiblePaths.Add(nwnPath);
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Registry access failed: {ex.Message}. Using hardcoded paths.");
            }

            // Common Steam locations
            possiblePaths.AddRange(new[]
            {
                @"C:\Program Files (x86)\Steam\steamapps\common\Neverwinter Nights",
                @"C:\Program Files\Steam\steamapps\common\Neverwinter Nights",
                @"D:\SteamLibrary\steamapps\common\Neverwinter Nights",
                @"E:\SteamLibrary\steamapps\common\Neverwinter Nights"
            });

            // GOG paths
            possiblePaths.AddRange(new[]
            {
                @"C:\Program Files (x86)\GOG Galaxy\Games\Neverwinter Nights Enhanced Edition",
                @"C:\GOG Games\Neverwinter Nights Enhanced Edition"
            });

            // Beamdog
            possiblePaths.Add(@"C:\Program Files\Beamdog\Neverwinter Nights");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            possiblePaths.Add(Path.Combine(home, ".steam", "steam", "steamapps", "common", "Neverwinter Nights"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            possiblePaths.Add("/Applications/Neverwinter Nights.app/Contents/Resources");
        }

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path) && Directory.Exists(Path.Combine(path, "data")))
            {
                return path;
            }
        }

        return null;
    }

    private string? DetectUserDataPath()
    {
        var possiblePaths = new List<string>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Primary: Documents\Neverwinter Nights
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            possiblePaths.Add(Path.Combine(docs, "Neverwinter Nights"));

            // Alternative: User profile\Documents
            possiblePaths.Add(Path.Combine(userProfile, "Documents", "Neverwinter Nights"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            possiblePaths.Add(Path.Combine(userProfile, ".local", "share", "Neverwinter Nights"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            possiblePaths.Add(Path.Combine(userProfile, "Library", "Application Support", "Neverwinter Nights"));
        }

        foreach (var path in possiblePaths)
        {
            // Validate by checking for characteristic NWN subdirectories
            if (Directory.Exists(path))
            {
                var ambientPath = Path.Combine(path, "ambient");
                var musicPath = Path.Combine(path, "music");
                if (Directory.Exists(ambientPath) && Directory.Exists(musicPath))
                {
                    return path;
                }
                // Also accept if modules folder exists (alternative validation)
                var modulesPath = Path.Combine(path, "modules");
                if (Directory.Exists(modulesPath))
                {
                    return path;
                }
            }
        }

        return null;
    }

    #endregion
}
