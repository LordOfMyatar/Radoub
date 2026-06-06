using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Radoub.Formats.Services;
using Radoub.UI.Controls;

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
    }

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
