using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using RadoubLauncher.Services;
using ToolTipService = Avalonia.Controls.ToolTip;

namespace RadoubLauncher.Controls;

public partial class ToolCardControl : UserControl
{
    public static readonly StyledProperty<ICommand?> LaunchToolCommandProperty =
        AvaloniaProperty.Register<ToolCardControl, ICommand?>(nameof(LaunchToolCommand));

    public static readonly StyledProperty<ICommand?> LaunchToolWithFileCommandProperty =
        AvaloniaProperty.Register<ToolCardControl, ICommand?>(nameof(LaunchToolWithFileCommand));

    public ICommand? LaunchToolCommand
    {
        get => GetValue(LaunchToolCommandProperty);
        set => SetValue(LaunchToolCommandProperty, value);
    }

    public ICommand? LaunchToolWithFileCommand
    {
        get => GetValue(LaunchToolWithFileCommandProperty);
        set => SetValue(LaunchToolWithFileCommandProperty, value);
    }

    public ToolCardControl()
    {
        InitializeComponent();

        // Wire up main button click to launch tool
        MainToolButton.Click += OnMainButtonClick;
    }

    private void OnMainButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ToolInfo tool && LaunchToolCommand?.CanExecute(tool) == true)
        {
            LaunchToolCommand.Execute(tool);
        }
    }

    private void OnRecentFilesButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ToolInfo tool)
            return;

        // Create context menu dynamically
        var contextMenu = new ContextMenu();

        // Add "Launch App Only" option first
        var launchAppItem = new MenuItem
        {
            Header = "Launch App Only",
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        };
        launchAppItem.Click += (_, _) =>
        {
            if (LaunchToolCommand?.CanExecute(tool) == true)
            {
                LaunchToolCommand.Execute(tool);
            }
        };
        contextMenu.Items.Add(launchAppItem);

        // Get recent files for this tool
        var recentFiles = ToolRecentFilesService.Instance.GetRecentFiles(tool.Name);

        if (recentFiles.Count > 0)
        {
            // Add separator
            contextMenu.Items.Add(new Separator());

            // Add recent files
            foreach (var file in recentFiles)
            {
                var menuItem = new MenuItem
                {
                    Header = file.DisplayName
                };
                // Show full path on hover
                ToolTipService.SetTip(menuItem, file.FullPath);

                // Capture the file path for the closure
                var filePath = file.FullPath;
                menuItem.Click += (_, _) =>
                {
                    var launchInfo = new ToolFileLaunchInfo
                    {
                        Tool = tool,
                        FilePath = filePath
                    };
                    if (LaunchToolWithFileCommand?.CanExecute(launchInfo) == true)
                    {
                        LaunchToolWithFileCommand.Execute(launchInfo);
                    }
                };

                contextMenu.Items.Add(menuItem);
            }
        }
        else
        {
            // No recent files - add disabled placeholder
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem
            {
                Header = "(No recent files)",
                IsEnabled = false,
                FontStyle = Avalonia.Media.FontStyle.Italic
            });
        }

        // Show the context menu at the button location
        contextMenu.Open(RecentFilesButton);
    }
}
