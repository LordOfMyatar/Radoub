using System;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Radoub.Formats.Logging;

namespace RadoubLauncher.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        LoadVersionInfo();
    }

    private void LoadVersionInfo()
    {
        var versionTextBlock = this.FindControl<TextBlock>("VersionTextBlock");
        if (versionTextBlock != null)
        {
            try
            {
                var infoVersion = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

                if (!string.IsNullOrEmpty(infoVersion))
                {
                    // Strip git hash if present
                    var plusIndex = infoVersion.IndexOf('+');
                    if (plusIndex > 0)
                        infoVersion = infoVersion[..plusIndex];

                    versionTextBlock.Text = $"Version {infoVersion}";
                }
            }
            catch
            {
                // Keep default text
            }
        }
    }

    private void OnGitHubLinkClick(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/LordOfMyatar/Radoub",
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
