using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using System.Collections.Generic;
using Radoub.Formats.Services;
using Radoub.UI.Controls;
using Radoub.UI.Utils;
using PlaceableEditor.Services;

namespace PlaceableEditor.Views.Panels;

/// <summary>
/// Identity / combat / flags panel (design §5.1). DataContext is the PlaceableViewModel; all
/// fields bind two-way. The panel stays thin: it raises <see cref="PortraitBrowseRequested"/> and
/// <see cref="AppearanceChanged"/> for the host (MainWindow) to service with shared dialogs and the
/// undo manager, and exposes <see cref="PopulateAppearances"/> / preview setters the host drives.
/// </summary>
public partial class IdentityCombatPanel : UserControl
{
    private bool _suppressAppearanceEvent;

    /// <summary>Raised when the user clicks the portrait Browse button.</summary>
    public event EventHandler? PortraitBrowseRequested;

    /// <summary>Raised when the user picks a different appearance (carries the new appearance id).</summary>
    public event EventHandler<uint>? AppearanceChanged;

    /// <summary>Raised when the user picks a different palette category (carries the new PaletteID).</summary>
    public event EventHandler<byte>? PaletteCategoryChanged;

    private bool _suppressCategoryEvent;

    public IdentityCombatPanel()
    {
        InitializeComponent();
        var name = this.FindControl<TextBox>("NameTextBox");
        if (name != null) name.TextChanged += OnNameChanged;
    }

    // --- Tag/ResRef sync with name (#2372), mirroring Relique's NewItem naming UX. One checkbox
    //     drives both fields (#2354 follow-up): checked → Tag/ResRef track the name and lock. ---

    // ResRef is the on-disk identity. For an already-saved placeable it must be read-only — editing
    // it desyncs the F4 browser row from the file on disk (renaming a saved file goes through the
    // browser's Rename flow instead). It stays editable only for a brand-new, never-saved placeable
    // so the first Save can name the file. Set via SetResRefLocked (host drives it on load). The
    // proper open-file rename flow is tracked separately.
    private bool _resRefLocked;

    /// <summary>
    /// Lock/unlock the ResRef field. Locked (saved file): always read-only, and the name-sync
    /// checkbox no longer re-derives ResRef (only Tag). Unlocked (new file): ResRef follows the
    /// sync checkbox as before.
    /// </summary>
    public void SetResRefLocked(bool locked)
    {
        _resRefLocked = locked;
        var resRef = this.FindControl<TextBox>("ResRefTextBox");
        if (resRef == null) return;
        if (locked)
        {
            resRef.IsReadOnly = true;
        }
        else
        {
            // New file: ResRef read-only only while the sync checkbox is on.
            resRef.IsReadOnly = this.FindControl<CheckBox>("SyncNameCheck")?.IsChecked == true;
        }
    }

    private void OnNameChanged(object? sender, TextChangedEventArgs e) => ApplyNameSync();

    private void OnSyncNameChanged(object? sender, RoutedEventArgs e)
    {
        var on = this.FindControl<CheckBox>("SyncNameCheck")?.IsChecked == true;
        var tag = this.FindControl<TextBox>("TagTextBox");
        var resRef = this.FindControl<TextBox>("ResRefTextBox");
        if (tag != null) tag.IsReadOnly = on;
        // A locked (saved) ResRef stays read-only regardless of the sync checkbox.
        if (resRef != null) resRef.IsReadOnly = on || _resRefLocked;
        if (on) ApplyNameSync();
    }

    /// <summary>Re-derive Tag (always) and ResRef (only when not locked) from the name while sync is on.</summary>
    private void ApplyNameSync()
    {
        if (this.FindControl<CheckBox>("SyncNameCheck")?.IsChecked != true) return;
        this.FindControl<TextBox>("TagTextBox")!.Text = PlaceableNamingService.GenerateTag(CurrentName);
        if (!_resRefLocked)
            this.FindControl<TextBox>("ResRefTextBox")!.Text = PlaceableNamingService.GenerateResRef(CurrentName);
    }

    private string CurrentName => this.FindControl<TextBox>("NameTextBox")?.Text ?? string.Empty;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>The hosted 3D preview control (host loads the placeable MDL into it).</summary>
    public ModelPreviewGLControl Preview => this.FindControl<ModelPreviewGLControl>("ModelPreview")!;

    /// <summary>Fill the appearance combo from the shared placeable appearance service.</summary>
    public void PopulateAppearances(IPlaceableAppearanceService appearances, uint selectedId)
    {
        var combo = this.FindControl<ComboBox>("AppearanceCombo");
        if (combo is null) return;

        _suppressAppearanceEvent = true;
        try
        {
            combo.ItemsSource = appearances.GetAll();
            combo.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(PlaceableAppearance.DisplayName));
            foreach (var item in appearances.GetAll())
            {
                if (item.Id == selectedId) { combo.SelectedItem = item; break; }
            }
        }
        finally
        {
            _suppressAppearanceEvent = false;
        }
    }

    /// <summary>
    /// Fill the palette-category combo from the placeable palette skeleton via the shared binder
    /// (#2416). Categories come from the host's IGameDataService (placeablepal.itp) — never hardcoded.
    /// </summary>
    public void PopulatePaletteCategories(IReadOnlyList<PaletteCategory>? categories, byte selectedId)
    {
        var combo = this.FindControl<ComboBox>("PaletteCategoryCombo");
        if (combo is null) return;

        _suppressCategoryEvent = true;
        try
        {
            PaletteCategoryComboBinder.Populate(combo, categories);
            PaletteCategoryComboBinder.SelectById(combo, selectedId);
        }
        finally
        {
            _suppressCategoryEvent = false;
        }
    }

    private void OnPaletteCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressCategoryEvent) return;
        var id = PaletteCategoryComboBinder.GetSelectedId(sender as ComboBox);
        if (id.HasValue) PaletteCategoryChanged?.Invoke(this, id.Value);
    }

    /// <summary>Set the portrait preview image (host resolves the bitmap from PortraitId).</summary>
    public void SetPortrait(Bitmap? bitmap)
    {
        var image = this.FindControl<Image>("PortraitImage");
        if (image != null) image.Source = bitmap;
    }

    private void OnPortraitBrowseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => PortraitBrowseRequested?.Invoke(this, EventArgs.Empty);

    private void OnAppearanceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressAppearanceEvent) return;
        if (sender is ComboBox { SelectedItem: PlaceableAppearance appearance })
            AppearanceChanged?.Invoke(this, appearance.Id);
    }
}
