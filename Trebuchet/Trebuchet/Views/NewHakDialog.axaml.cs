using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Radoub.UI.Services;

namespace RadoubLauncher.Views;

/// <summary>
/// Non-modal dialog to gather the inputs for a new HAK archive: name (Aurora-validated),
/// optional description, and output folder (#2267). Raises <see cref="Confirmed"/> when the
/// user commits valid input; the caller performs the actual file creation.
/// </summary>
public partial class NewHakDialog : Window
{
    /// <summary>Raised once, when the user confirms with a valid name and chosen folder.</summary>
    public event EventHandler? Confirmed;

    public string HakName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string OutputFolder { get; private set; } = string.Empty;

    /// <summary>True when the user opted to register the HAK in the open module's IFO HAK list.</summary>
    public bool AddToModuleIfo { get; private set; }

    public NewHakDialog()
    {
        InitializeComponent();
    }

    /// <param name="moduleName">Open module's name, or null when no module is loaded. Drives the
    /// "add to module IFO" checkbox — disabled with an explanatory tooltip when null (#2267).</param>
    public NewHakDialog(string? moduleName) : this()
    {
        if (string.IsNullOrEmpty(moduleName))
        {
            AddToIfoCheckBox.IsEnabled = false;
            AddToIfoCheckBox.Content = "Add to open module (IFO HAK list)";
            ToolTip.SetTip(AddToIfoCheckBox, "Open a module first to register this HAK in its IFO.");
        }
        else
        {
            AddToIfoCheckBox.Content = $"Add to '{moduleName}' module (IFO HAK list)";
        }
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var storage = GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose output folder for the new HAK",
            AllowMultiple = false,
        });

        if (folders.Count > 0)
            FolderTextBox.Text = folders[0].Path.LocalPath;
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim() ?? string.Empty;
        var folder = FolderTextBox.Text?.Trim() ?? string.Empty;

        var validation = AuroraFilenameValidator.Validate(name);
        if (!validation.IsValid)
        {
            ShowNameError(validation.GetErrorMessage());
            return;
        }

        if (string.IsNullOrEmpty(folder))
        {
            ShowNameError("Choose an output folder.");
            return;
        }

        HakName = name;
        Description = DescriptionTextBox.Text?.Trim() ?? string.Empty;
        OutputFolder = folder;
        AddToModuleIfo = AddToIfoCheckBox.IsEnabled && AddToIfoCheckBox.IsChecked == true;

        Confirmed?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void ShowNameError(string message)
    {
        NameError.Text = message;
        NameError.IsVisible = true;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
