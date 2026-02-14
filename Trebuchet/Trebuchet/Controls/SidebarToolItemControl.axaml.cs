using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using RadoubLauncher.Services;

namespace RadoubLauncher.Controls;

public partial class SidebarToolItemControl : UserControl
{
    public static readonly StyledProperty<ICommand?> LaunchToolCommandProperty =
        AvaloniaProperty.Register<SidebarToolItemControl, ICommand?>(nameof(LaunchToolCommand));

    public ICommand? LaunchToolCommand
    {
        get => GetValue(LaunchToolCommandProperty);
        set => SetValue(LaunchToolCommandProperty, value);
    }

    public SidebarToolItemControl()
    {
        InitializeComponent();

        ToolButton.Click += OnToolButtonClick;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ToolInfo tool)
        {
            var statusKey = tool.IsAvailable ? "ThemeSuccess" : "ThemeDisabled";
            StatusDot[!Border.BackgroundProperty] = new DynamicResourceExtension(statusKey);

            var maturityKey = tool.Maturity switch
            {
                ToolMaturity.Stable => "ThemeSuccess",
                ToolMaturity.Beta => "ThemeInfo",
                ToolMaturity.Alpha => "ThemeWarning",
                ToolMaturity.InDevelopment => "ThemeDisabled",
                _ => "ThemeDisabled"
            };
            MaturityBadge[!Border.BackgroundProperty] = new DynamicResourceExtension(maturityKey);
        }
    }

    private void OnToolButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ToolInfo tool && LaunchToolCommand?.CanExecute(tool) == true)
        {
            LaunchToolCommand.Execute(tool);
        }
    }
}
