using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DialogEditor.Utils;
using System;
using System.Diagnostics;

namespace DialogEditor.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            // Set version text dynamically
            var versionTextBlock = this.FindControl<TextBlock>("VersionTextBlock");
            if (versionTextBlock != null)
            {
                versionTextBlock.Text = $"Version {VersionHelper.FullVersion} (Alpha)";
            }
        }

        private void OnGitHubLinkClick(object? sender, PointerPressedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/LordOfMyatar/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                DialogEditor.Services.UnifiedLogger.LogApplication(
                    DialogEditor.Services.LogLevel.WARN,
                    $"Failed to open browser for GitHub link: {ex.Message}");
            }
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
