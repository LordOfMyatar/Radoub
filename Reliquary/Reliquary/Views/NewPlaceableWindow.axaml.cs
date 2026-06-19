using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.UI.Services;

namespace PlaceableEditor.Views;

/// <summary>
/// Modal New Placeable dialog (#2418-style). Prompts for Name/Tag/ResRef with a "sync all three
/// from the Name" checkbox, mirroring Fence's NewStoreWindow. While sync is on, Tag and ResRef are
/// derived live from the Name (Aurora-sanitized via <see cref="BlueprintNamingService"/>) and are
/// read-only; unchecking frees them so the user can override Tag and ResRef individually. Confirm
/// exposes <see cref="Result"/>; cancel leaves it null.
///
/// Modal is correct here (resource-pick-style carve-out): the New flow has no work to do until the
/// user confirms or cancels.
/// </summary>
public partial class NewPlaceableWindow : Window
{
    /// <summary>The confirmed values, or null if the user cancelled.</summary>
    public NewPlaceableResult? Result { get; private set; }

    public NewPlaceableWindow()
    {
        InitializeComponent();

        NameBox.TextChanged += OnNameChanged;
        TagBox.TextChanged += OnTagChanged;
        ResRefBox.TextChanged += OnResRefChanged;
        SyncCheckBox.IsCheckedChanged += OnSyncChanged;

        // Sensible defaults so a bare Confirm still produces a valid placeable.
        NameBox.Text = "New Placeable";
        ApplySync();

        // Focus and SELECT ALL so the user's first keystroke replaces the default rather than
        // prepending to it (caret-at-0 would give "mynameNew Placeable").
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
        var name = (NameBox.Text ?? string.Empty).Trim();
        var tag = TagBox.Text ?? string.Empty;
        var resRef = ResRefBox.Text ?? string.Empty;

        string? error = null;
        if (string.IsNullOrEmpty(name))
            error = "Name is required.";
        else if (!string.IsNullOrEmpty(tag) && !BlueprintNamingService.IsValidTag(tag))
            error = "Tag must be 1-32 characters (A-Z, 0-9, underscore).";
        else if (string.IsNullOrEmpty(resRef))
            error = "ResRef is required.";
        else if (!BlueprintNamingService.IsValidResRef(resRef))
            error = "ResRef must be 1-16 lowercase alphanumeric/underscore characters.";

        ValidationText.Text = error ?? string.Empty;
        CreateButton.IsEnabled = error is null;
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        var name = (NameBox.Text ?? string.Empty).Trim();
        var tag = TagBox.Text ?? string.Empty;
        var resRef = ResRefBox.Text ?? string.Empty;

        // Guard: Create may be triggered by Enter even if validation failed.
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(resRef)
            || !BlueprintNamingService.IsValidResRef(resRef)
            || !BlueprintNamingService.IsValidTag(tag))
        {
            UpdateValidation();
            return;
        }

        Result = new NewPlaceableResult(name, tag, resRef);
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}

/// <summary>Confirmed values from the New Placeable dialog.</summary>
public readonly record struct NewPlaceableResult(string Name, string Tag, string ResRef);
