using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.UI.Services;

namespace Radoub.UI.Views;

/// <summary>
/// Shared modal New-blueprint dialog (#2517). Prompts for Name/Tag/ResRef with a "sync all three
/// from the Name" checkbox. While sync is on, Tag and ResRef are derived live from the Name
/// (Aurora-sanitized via <see cref="BlueprintNamingService"/>) and are read-only; unchecking frees
/// them for individual editing. Confirm exposes <see cref="Result"/>; cancel leaves it null.
///
/// Consolidates Fence's NewStoreWindow and Reliquary's NewPlaceableWindow (near-duplicates) into a
/// single control parameterized by title/header/watermark/default-name. Modal is correct here
/// (resource-pick-style carve-out): the New flow has no work to do until the user confirms or cancels.
/// </summary>
public partial class NewBlueprintWindow : Window
{
    /// <summary>The confirmed values, or null if the user cancelled.</summary>
    public NewBlueprintResult? Result { get; private set; }

    /// <summary>Parameterless ctor for the XAML designer / InitializeComponent.</summary>
    public NewBlueprintWindow() : this("New Blueprint", "Create a new blueprint", "e.g. New Blueprint", "New Blueprint")
    {
    }

    /// <summary>
    /// Create the dialog with tool-specific labels.
    /// </summary>
    /// <param name="title">Window title (e.g. "New Store - Fence").</param>
    /// <param name="header">Header text (e.g. "Create a new store").</param>
    /// <param name="nameWatermark">Watermark for the Name box (e.g. "e.g. General Store").</param>
    /// <param name="defaultName">Default Name value so a bare Confirm still produces a valid blueprint.</param>
    public NewBlueprintWindow(string title, string header, string nameWatermark, string defaultName)
    {
        InitializeComponent();

        Title = title;
        HeaderText.Text = header;
        NameBox.Watermark = nameWatermark;

        NameBox.TextChanged += OnNameChanged;
        TagBox.TextChanged += OnTagChanged;
        ResRefBox.TextChanged += OnResRefChanged;
        SyncCheckBox.IsCheckedChanged += OnSyncChanged;

        // Sensible defaults so a bare Confirm still produces a valid blueprint.
        NameBox.Text = defaultName;
        ApplySync();

        // Focus and SELECT ALL so the user's first keystroke replaces the default rather than
        // prepending to it (caret-at-0 would give "mynameNew Blueprint").
        NameBox.AttachedToVisualTree += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private bool IsSyncOn => SyncCheckBox.IsChecked == true;

    private void OnSyncChanged(object? sender, RoutedEventArgs e)
    {
        ApplySync();
    }

    /// <summary>Apply the current sync state: when on, derive Tag/ResRef from Name and lock them.</summary>
    private void ApplySync()
    {
        var syncOn = IsSyncOn;
        TagBox.IsReadOnly = syncOn;
        ResRefBox.IsReadOnly = syncOn;

        if (syncOn)
        {
            var name = NameBox.Text ?? string.Empty;
            TagBox.Text = BlueprintNamingService.GenerateTag(name);
            ResRefBox.Text = BlueprintNamingService.GenerateResRef(name);
        }

        UpdateValidation();
    }

    private void OnNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsSyncOn)
        {
            var name = NameBox.Text ?? string.Empty;
            TagBox.Text = BlueprintNamingService.GenerateTag(name);
            ResRefBox.Text = BlueprintNamingService.GenerateResRef(name);
        }
        UpdateValidation();
    }

    private void OnTagChanged(object? sender, TextChangedEventArgs e)
    {
        TagCharCount.Text = $"{(TagBox.Text ?? string.Empty).Length}/32";
        UpdateValidation();
    }

    private void OnResRefChanged(object? sender, TextChangedEventArgs e)
    {
        ResRefCharCount.Text = $"{(ResRefBox.Text ?? string.Empty).Length}/16";
        UpdateValidation();
    }

    /// <summary>Validate the current field values and enable/disable Create accordingly.</summary>
    private void UpdateValidation()
    {
        var error = NewBlueprintValidation.Validate(NameBox.Text, TagBox.Text, ResRefBox.Text);
        ValidationText.Text = error ?? string.Empty;
        CreateButton.IsEnabled = error is null;
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        var name = (NameBox.Text ?? string.Empty).Trim();
        var tag = TagBox.Text ?? string.Empty;
        var resRef = ResRefBox.Text ?? string.Empty;

        // Guard: Create may be triggered by Enter even if validation failed.
        if (NewBlueprintValidation.Validate(name, tag, resRef) is not null)
        {
            UpdateValidation();
            return;
        }

        Result = new NewBlueprintResult(name, tag, resRef);
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}

/// <summary>Confirmed values from the shared New-blueprint dialog.</summary>
public readonly record struct NewBlueprintResult(string Name, string Tag, string ResRef);
