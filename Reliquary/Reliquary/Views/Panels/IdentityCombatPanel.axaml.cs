using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Radoub.Formats.Services;
using Radoub.UI.Controls;
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

    public IdentityCombatPanel()
    {
        InitializeComponent();
        var name = this.FindControl<TextBox>("NameTextBox");
        if (name != null) name.TextChanged += OnNameChanged;
    }

    // --- Tag/ResRef sync with name (#2372), mirroring Relique's NewItem naming UX ---

    private void OnNameChanged(object? sender, TextChangedEventArgs e) => ApplyNameSync();

    private void OnSyncTagChanged(object? sender, RoutedEventArgs e)
    {
        var tag = this.FindControl<TextBox>("TagTextBox");
        var check = this.FindControl<CheckBox>("SyncTagCheck");
        if (tag is null || check is null) return;
        tag.IsReadOnly = check.IsChecked == true;
        if (check.IsChecked == true) tag.Text = PlaceableNamingService.GenerateTag(CurrentName);
    }

    private void OnSyncResRefChanged(object? sender, RoutedEventArgs e)
    {
        var resRef = this.FindControl<TextBox>("ResRefTextBox");
        var check = this.FindControl<CheckBox>("SyncResRefCheck");
        if (resRef is null || check is null) return;
        resRef.IsReadOnly = check.IsChecked == true;
        if (check.IsChecked == true) resRef.Text = PlaceableNamingService.GenerateResRef(CurrentName);
    }

    /// <summary>Re-derive Tag/ResRef from the current name for whichever sync checkboxes are on.</summary>
    private void ApplyNameSync()
    {
        if (this.FindControl<CheckBox>("SyncTagCheck")?.IsChecked == true)
            this.FindControl<TextBox>("TagTextBox")!.Text = PlaceableNamingService.GenerateTag(CurrentName);
        if (this.FindControl<CheckBox>("SyncResRefCheck")?.IsChecked == true)
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
