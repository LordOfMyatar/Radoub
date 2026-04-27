using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.UI.Services;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Controls;

public partial class LaunchTestPanel : UserControl
{
    private MainWindowViewModel? _viewModel;

    public LaunchTestPanel()
    {
        InitializeComponent();
        Unloaded += OnPanelUnloaded;
    }

    /// <summary>
    /// Initialize the panel with the main ViewModel.
    /// Called by MainWindow after embedding the panel.
    /// </summary>
    public void Initialize(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateBuildWarningColors();
        UpdateFailedScriptsColors();
        UpdateStaleScriptText();
        UpdateModLockedWarningColor();
    }

    private void OnPanelUnloaded(object? sender, RoutedEventArgs e)
    {
        Unloaded -= OnPanelUnloaded;
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.NeedsBuildWarning))
            UpdateBuildWarningColors();

        if (e.PropertyName == nameof(MainWindowViewModel.StaleScriptCount))
            UpdateStaleScriptText();

        if (e.PropertyName == nameof(MainWindowViewModel.HasFailedScripts))
            UpdateFailedScriptsColors();

        if (e.PropertyName == nameof(MainWindowViewModel.IsModFileLocked))
            UpdateModLockedWarningColor();
    }

    private void UpdateBuildWarningColors()
    {
        if (_viewModel == null) return;

        var warningIcon = this.FindControl<TextBlock>("BuildWarningIcon");
        var warningText = this.FindControl<TextBlock>("BuildWarningTextBlock");
        var okIcon = this.FindControl<TextBlock>("BuildOkIcon");

        if (warningIcon != null)
            warningIcon.Foreground = BrushManager.GetWarningBrush(this);

        if (warningText != null)
            warningText.Foreground = BrushManager.GetWarningBrush(this);

        if (okIcon != null)
            okIcon.Foreground = BrushManager.GetSuccessBrush(this);
    }

    private void UpdateFailedScriptsColors()
    {
        var failedIcon = this.FindControl<TextBlock>("FailedScriptsIcon");
        if (failedIcon != null)
            failedIcon.Foreground = BrushManager.GetErrorBrush(this);
    }

    private void UpdateModLockedWarningColor()
    {
        var lockedWarning = this.FindControl<TextBlock>("ModLockedWarning");
        if (lockedWarning != null)
            lockedWarning.Foreground = BrushManager.GetWarningBrush(this);
    }

    private void UpdateStaleScriptText()
    {
        if (_viewModel == null) return;

        var staleText = this.FindControl<TextBlock>("StaleScriptText");
        if (staleText == null) return;

        var count = _viewModel.StaleScriptCount;
        if (count > 0)
        {
            staleText.Text = $"{count} script(s) have .nss files newer than their compiled .ncs";
            staleText.IsVisible = true;
        }
        else
        {
            staleText.IsVisible = false;
        }
    }
}
