using System;
using Avalonia.Controls;
using RadoubLauncher.Services;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Views;

/// <summary>
/// First-run / welcome-back configuration wizard window (#1020).
/// </summary>
public partial class FirstRunWizardWindow : Window
{
    private FirstRunWizardViewModel? _viewModel;

    public FirstRunWizardWindow()
    {
        InitializeComponent();
    }

    /// <summary>Initialize for the given mode. Call before showing.</summary>
    public void Initialize(WizardMode mode)
    {
        _viewModel = new FirstRunWizardViewModel(this, mode);
        _viewModel.Completed += (_, _) => Close();
        _viewModel.Cancelled += (_, _) => Close();
        DataContext = _viewModel;
    }
}
