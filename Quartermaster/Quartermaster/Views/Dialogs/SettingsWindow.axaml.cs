using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Settings window: module configuration, resource paths (Paths.cs), UI settings, cache management.
/// </summary>
public partial class SettingsWindow : Window
{
    private bool _isInitializing = true;
    private MainWindow? _mainWindow;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
        UpdateCacheInfo();
        _isInitializing = false;
    }

    /// <summary>
    /// Set the main window reference for cache operations.
    /// </summary>
    public void SetMainWindow(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        UpdateCacheInfo();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void LoadSettings()
    {
        LoadModuleConfiguration();
        LoadResourcePathSettings();
        LoadUISettings();
    }

    #region Module Configuration (#1325, #1322)

    private void LoadModuleConfiguration()
    {
        var currentModuleText = this.FindControl<TextBlock>("CurrentModuleText");
        if (currentModuleText == null) return;

        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (RadoubSettings.IsValidModulePath(modulePath))
        {
            // Try to get module name from IFO
            string? workingDir = modulePath;
            if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
            {
                // Resolve .mod to unpacked dir for IFO reading
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

            currentModuleText.Text = displayName ?? Path.GetFileName(modulePath);
            currentModuleText.Foreground = BrushManager.GetInfoBrush(this);
        }
        else
        {
            currentModuleText.Text = "No module selected";
            currentModuleText.Foreground = BrushManager.GetWarningBrush(this);
        }
    }

    private void OnConfigureInTrebuchetClick(object? sender, RoutedEventArgs e)
    {
        var trebuchetPath = RadoubSettings.Instance.TrebuchetPath;
        var statusText = this.FindControl<TextBlock>("TrebuchetStatusText");

        if (string.IsNullOrEmpty(trebuchetPath) || !File.Exists(trebuchetPath))
        {
            if (statusText != null)
            {
                statusText.Text = "Trebuchet not found. Launch Trebuchet once to register its path.";
                statusText.Foreground = BrushManager.GetWarningBrush(this);
            }
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = trebuchetPath,
                UseShellExecute = true
            });

            if (statusText != null)
            {
                statusText.Text = "Trebuchet launched. Restart this tool after changing module.";
                statusText.Foreground = BrushManager.GetInfoBrush(this);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to launch Trebuchet: {ex.Message}");
            if (statusText != null)
            {
                statusText.Text = $"Failed to launch Trebuchet: {ex.Message}";
                statusText.Foreground = BrushManager.GetErrorBrush(this);
            }
        }
    }

    #endregion

    #region UI Settings

    private void LoadUISettings()
    {
        // Theme and font settings are now managed centrally by Trebuchet/RadoubSettings.
        // No per-tool theme/font UI needed.
    }

    #endregion

    #region Cache Management

    private void UpdateCacheInfo()
    {
        var cacheStatusText = this.FindControl<TextBlock>("CacheStatusText");
        var cacheItemCountText = this.FindControl<TextBlock>("CacheItemCountText");
        var cacheSizeText = this.FindControl<TextBlock>("CacheSizeText");
        var cacheSourcesText = this.FindControl<TextBlock>("CacheSourcesText");

        if (cacheStatusText == null) return;

        var stats = _mainWindow?.GetPaletteCacheStatistics();

        if (stats != null && stats.TotalItems > 0)
        {
            cacheStatusText.Text = "Cached";
            cacheStatusText.Foreground = GetSuccessBrush();

            if (cacheItemCountText != null)
                cacheItemCountText.Text = $"{stats.TotalItems:N0}";

            if (cacheSizeText != null)
                cacheSizeText.Text = $"{stats.TotalSizeKB:N1} KB";

            if (cacheSourcesText != null)
            {
                var sources = stats.SourceCounts
                    .Select(kv => $"{kv.Key}: {kv.Value:N0}")
                    .ToList();
                cacheSourcesText.Text = sources.Count > 0 ? string.Join(", ", sources) : "-";
            }
        }
        else
        {
            cacheStatusText.Text = "No cache";
            cacheStatusText.Foreground = GetWarningBrush();

            if (cacheItemCountText != null)
                cacheItemCountText.Text = "-";

            if (cacheSizeText != null)
                cacheSizeText.Text = "-";

            if (cacheSourcesText != null)
                cacheSourcesText.Text = "-";
        }
    }

    private async void OnClearCacheClick(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow == null)
            return;

        var cacheStatusText = this.FindControl<TextBlock>("CacheStatusText");
        var clearCacheButton = this.FindControl<Button>("ClearCacheButton");

        if (cacheStatusText != null)
        {
            cacheStatusText.Text = "Rebuilding...";
            cacheStatusText.Foreground = GetWarningBrush();
        }

        if (clearCacheButton != null)
            clearCacheButton.IsEnabled = false;

        try
        {
            await _mainWindow.ClearAndReloadPaletteCacheAsync();

            if (cacheStatusText != null)
            {
                cacheStatusText.Text = "Rebuilt";
                cacheStatusText.Foreground = GetSuccessBrush();
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Cache rebuild failed: {ex.Message}");

            if (cacheStatusText != null)
            {
                cacheStatusText.Text = "Rebuild failed";
                cacheStatusText.Foreground = GetErrorBrush();
            }
        }
        finally
        {
            if (clearCacheButton != null)
                clearCacheButton.IsEnabled = true;

            UpdateCacheInfo();
        }
    }

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

    private void ApplySettings()
    {
        var sharedSettings = RadoubSettings.Instance;

        // Resource paths
        var baseGamePathTextBox = this.FindControl<TextBox>("BaseGamePathTextBox");
        if (baseGamePathTextBox != null)
        {
            var path = baseGamePathTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                sharedSettings.BaseGameInstallPath = path;
            }
        }

        var gamePathTextBox = this.FindControl<TextBox>("GamePathTextBox");
        if (gamePathTextBox != null)
        {
            var path = gamePathTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                sharedSettings.NeverwinterNightsPath = path;
            }
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, "Settings applied successfully");
    }

    #endregion

    #region Theme-Aware Colors

    private IBrush GetErrorBrush() => BrushManager.GetErrorBrush(this);
    private IBrush GetSuccessBrush() => BrushManager.GetSuccessBrush(this);
    private IBrush GetWarningBrush() => BrushManager.GetWarningBrush(this);

    #endregion
}
