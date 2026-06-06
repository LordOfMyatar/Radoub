using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Logging;

namespace PlaceableEditor.Views;

/// <summary>
/// Reliquary main window. Sprint 4 ships a demoable skeleton: browser sidebar +
/// four stubbed content panels. Editor wiring lands in Sprints 5-6. Organized as
/// partial classes (QM/Relique pattern): this file owns construction and menu
/// handlers; MainWindow.Lifecycle.cs owns window/browser lifecycle.
/// </summary>
public partial class MainWindow : Window
{
    private string? _currentFilePath;

    public MainWindow()
    {
        InitializeComponent();
        WireBrowserPanel();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void UpdateStatus(string message)
    {
        var status = this.FindControl<TextBlock>("StatusBar");
        if (status != null)
            status.Text = message;
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        // File open wiring lands in Sprint 5. The browser sidebar is the primary
        // open path for the skeleton; this menu item is a placeholder.
        UpdateStatus("Open from the browser sidebar — File > Open wiring lands in Sprint 5.");
        await System.Threading.Tasks.Task.CompletedTask;
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var aboutWindow = Radoub.UI.Views.AboutWindow.Create(new Radoub.UI.Views.AboutWindowConfig
            {
                ToolName = "Reliquary",
                Version = Radoub.UI.Utils.VersionHelper.GetVersion(),
                Subtitle = "Placeable Blueprint Editor"
            });
            aboutWindow.Show(this);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Reliquary: About window failed: {ex.Message}");
        }
    }
}
