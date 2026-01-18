using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Radoub.Formats.Logging;

namespace Radoub.UI.Views;

/// <summary>
/// Shared About window for all Radoub tools.
/// Configure via properties before showing.
/// </summary>
public partial class AboutWindow : Window
{
    private string _githubUrl = "https://github.com/LordOfMyatar/Radoub";

    public AboutWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Configure and show the About window with tool-specific info.
    /// </summary>
    public static AboutWindow Create(AboutWindowConfig config)
    {
        var window = new AboutWindow();
        window.Configure(config);
        return window;
    }

    private void Configure(AboutWindowConfig config)
    {
        // Window title
        Title = $"About {config.ToolName}";

        // App name
        var appNameText = this.FindControl<TextBlock>("AppNameText");
        if (appNameText != null)
            appNameText.Text = config.ToolName;

        // Subtitle
        var subtitleText = this.FindControl<TextBlock>("SubtitleText");
        if (subtitleText != null)
            subtitleText.Text = config.Subtitle ?? "Part of the Radoub Toolset";

        // Version
        var versionText = this.FindControl<TextBlock>("VersionText");
        if (versionText != null)
            versionText.Text = $"Version {config.Version}";

        // GitHub URL
        if (!string.IsNullOrEmpty(config.GitHubUrl))
        {
            _githubUrl = config.GitHubUrl;
            var githubLinkText = this.FindControl<TextBlock>("GitHubLinkText");
            if (githubLinkText != null)
                githubLinkText.Text = config.GitHubUrl;
        }

        // Icon
        var appIcon = this.FindControl<Image>("AppIcon");
        if (appIcon != null)
        {
            if (config.IconBitmap != null)
            {
                appIcon.Source = config.IconBitmap;
                appIcon.IsVisible = true;
            }
            else
            {
                appIcon.IsVisible = false;
            }
        }

        // Additional info
        var additionalInfoText = this.FindControl<TextBlock>("AdditionalInfoText");
        if (additionalInfoText != null && !string.IsNullOrEmpty(config.AdditionalInfo))
        {
            additionalInfoText.Text = config.AdditionalInfo;
            additionalInfoText.IsVisible = true;
        }
    }

    private void OnGitHubLinkClick(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _githubUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to open browser: {ex.Message}");
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// Configuration for the shared About window.
/// </summary>
public class AboutWindowConfig
{
    /// <summary>
    /// Tool name (e.g., "Parley", "Trebuchet")
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Version string (e.g., "0.1.0-alpha")
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Subtitle displayed under the tool name.
    /// Default: "Part of the Radoub Toolset"
    /// </summary>
    public string? Subtitle { get; init; }

    /// <summary>
    /// GitHub URL. Default: https://github.com/LordOfMyatar/Radoub
    /// </summary>
    public string? GitHubUrl { get; init; }

    /// <summary>
    /// Icon bitmap to display. If null, icon area is hidden.
    /// </summary>
    public Bitmap? IconBitmap { get; init; }

    /// <summary>
    /// Additional info text (tool-specific details, feature list, etc.)
    /// </summary>
    public string? AdditionalInfo { get; init; }
}
