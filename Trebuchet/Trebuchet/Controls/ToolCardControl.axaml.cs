using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
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

    private void OnFlyoutOpening(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout flyout || DataContext is not ToolInfo tool)
            return;

        // Clear existing items
        flyout.Items.Clear();

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
        flyout.Items.Add(launchAppItem);

        // Get recent files for this tool
        var recentFiles = ToolRecentFilesService.Instance.GetRecentFiles(tool.Name);

        if (recentFiles.Count > 0)
        {
            // Add separator
            flyout.Items.Add(new Separator());

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

                flyout.Items.Add(menuItem);
            }
        }
        else
        {
            // No recent files - add disabled placeholder
            flyout.Items.Add(new Separator());
            flyout.Items.Add(new MenuItem
            {
                Header = "(No recent files)",
                IsEnabled = false,
                FontStyle = Avalonia.Media.FontStyle.Italic
            });
        }
    }
}
