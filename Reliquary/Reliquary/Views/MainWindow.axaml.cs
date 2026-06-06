using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
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
        WireEditor();
        WireServices();
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
        var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open Placeable",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("Placeable Blueprint") { Patterns = new[] { "*.utp" } },
                new Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (!string.IsNullOrEmpty(path))
            LoadPlaceable(path);
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
