using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RadoubLauncher.Services;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Views;

/// <summary>
/// Non-modal window listing HAK conflicts for the current module (#1162).
/// </summary>
public partial class HakConflictWindow : Window
{
    private readonly HakConflictViewModel _viewModel = new();

    public HakConflictWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    /// <summary>
    /// Resolve the given HAK names against the search paths and run the conflict
    /// check off the UI thread (HAK files are read from disk), then populate the
    /// grid. Call after the window is shown.
    /// </summary>
    public async Task RunCheckAsync(IReadOnlyList<string> hakNamesInPriorityOrder, IReadOnlyList<string> hakSearchPaths)
    {
        _viewModel.StatusText = "Checking HAK files…";

        var report = await Task.Run(() =>
            HakConflictCheckerService.CheckHakNames(hakNamesInPriorityOrder, hakSearchPaths));

        await Dispatcher.UIThread.InvokeAsync(() => _viewModel.Load(report));
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
