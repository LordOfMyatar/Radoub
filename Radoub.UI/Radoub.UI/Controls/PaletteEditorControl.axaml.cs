using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;
using Radoub.UI.ViewModels;
using Radoub.UI.Views;

namespace Radoub.UI.Controls;

/// <summary>
/// The shared ITP palette editor view (#2477, M3): a resource-type selector, a single tree of
/// categories with blueprint leaves inline (plus the virtual Uncategorized bucket), drag-drop
/// reorganization, and save. The control is host-agnostic — Trebuchet (or a future per-tool host)
/// supplies the <see cref="PaletteEditorHostViewModel"/> via <see cref="Bind"/>. Category
/// add/rename/delete is deferred (needs a TLK-name decision, #2486); v1 is reorganize-only.
/// </summary>
public partial class PaletteEditorControl : UserControl
{
    private const double DragThreshold = 4.0;

    private PaletteEditorHostViewModel? _host;
    private Window? _ownerWindow;

    // Drag state: a press records a potential drag; a move past the threshold starts it. Starting
    // the drag on raw press fights the TreeView's own selection/expansion (the "can't click" bug).
    private PaletteNodeViewModel? _pendingDragNode;
    private Avalonia.Point _pressPoint;
    private bool _dragging;

    public PaletteEditorControl()
    {
        InitializeComponent();

        // Populate the resource-type selector from the enum.
        TypeSelector.ItemsSource = Enum.GetValues<PaletteResourceType>();
        TypeSelector.SelectedIndex = 0;

        DragDrop.SetAllowDrop(PaletteTree, true);
        PaletteTree.AddHandler(DragDrop.DropEvent, OnDrop);
        PaletteTree.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        // Tunnel so we see the press before the TreeView consumes it, but we DON'T start the drag
        // here — we only record a candidate, letting normal click/select/expand proceed.
        PaletteTree.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel);
        PaletteTree.AddHandler(PointerMovedEvent, OnTreePointerMoved, RoutingStrategies.Tunnel);
        PaletteTree.AddHandler(PointerReleasedEvent, OnTreePointerReleased, RoutingStrategies.Tunnel);
    }

    /// <summary>Bind the control to its host view-model and owning window (for modal prompts).</summary>
    public void Bind(PaletteEditorHostViewModel host, Window ownerWindow)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _ownerWindow = ownerWindow;
        DataContext = host;
        RefreshChrome();
    }

    // ---- type selector + load -----------------------------------------------

    private async void OnTypeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_host is null || TypeSelector.SelectedItem is not PaletteResourceType type) return;
        // If the switch is refused (dirty + cancel), snap the selector back to the active type.
        await _host.SwitchResourceTypeAsync(type);
        if (_host.ActiveContext is { } ctx && !Equals(TypeSelector.SelectedItem, ctx.Type))
            TypeSelector.SelectedItem = ctx.Type;
        RefreshChrome();
    }

    private async void OnReloadClick(object? sender, RoutedEventArgs e)
    {
        if (_host is null || TypeSelector.SelectedItem is not PaletteResourceType type) return;
        await _host.SwitchResourceTypeAsync(type);
        RefreshChrome();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_host is null) return;
        bool ok = _host.Save();
        RefreshChrome();
        if (!ok) NotifySaveFailed();
    }

    // ---- drag-drop (threshold-based; press records a candidate, move starts it) --------------

    private PaletteNodeViewModel? _activeDrag;

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pendingDragNode = null;
        if (_host is null) return;
        if ((e.Source as Control)?.DataContext is not PaletteNodeViewModel node) return;
        if (node.Kind is PaletteNodeKind.Uncategorized or PaletteNodeKind.Branch) return; // not draggable
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        // Record a candidate only; let the TreeView handle selection/expansion on this press.
        _pendingDragNode = node;
        _pressPoint = e.GetPosition(this);
    }

    private async void OnTreePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pendingDragNode is null || _dragging) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { _pendingDragNode = null; return; }

        var delta = e.GetPosition(this) - _pressPoint;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;

        _activeDrag = _pendingDragNode;
        _pendingDragNode = null;
        _dragging = true;
        try
        {
#pragma warning disable CS0618 // DataObject matches the ItemListView/EquipmentSlots drag pattern
            await DragDrop.DoDragDrop(e, new DataObject(), DragDropEffects.Move);
#pragma warning restore CS0618
        }
        finally
        {
            _dragging = false;
            _activeDrag = null;
        }
    }

    private void OnTreePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pendingDragNode = null; // a plain click/release without crossing the threshold: no drag
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = _activeDrag is null ? DragDropEffects.None : DragDropEffects.Move;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (_host is null || _activeDrag is null) return;
        if ((e.Source as Control)?.DataContext is not PaletteNodeViewModel target) return;

        var source = _activeDrag;

        // Resolve the destination category from the drop target:
        //  - onto a category => that category
        //  - onto a blueprint leaf => that leaf's parent category
        var destCategory = ResolveDropCategory(target);

        if (source.Kind == PaletteNodeKind.Blueprint)
        {
            if (destCategory is null) return; // can't file onto the Uncategorized bucket
            string resRef = source.Name;
            // The drag's `from` is the blueprint's current TREE home (null when uncategorized) —
            // never its PaletteID-derived category.
            var from = _host.ActiveContext?.ViewModel.Classify(resRef).Home;
            _host.MoveOrFileBlueprint(resRef, from, destCategory);
        }
        else if (source.Kind is PaletteNodeKind.Category && source.Model is PaletteCategoryNode cat)
        {
            // Nest under the destination category (cycle guard in the VM refuses invalid drops).
            if (destCategory is null || ReferenceEquals(destCategory, cat)) return;
            _host.MoveCategory(cat, destCategory, destCategory.Children.Count);
        }

        RefreshChrome();
    }

    // The category a drop lands in: a category target itself, or a blueprint leaf's parent category.
    private PaletteCategoryNode? ResolveDropCategory(PaletteNodeViewModel target)
    {
        if (target.Kind == PaletteNodeKind.Category && target.Model is PaletteCategoryNode c)
            return c;
        if (target.Kind == PaletteNodeKind.Blueprint && target.Model is PaletteBlueprintNode bp)
            return FindOwningCategory(bp);
        return null; // Uncategorized bucket or a branch
    }

    private PaletteCategoryNode? FindOwningCategory(PaletteBlueprintNode bp)
        => _host?.ActiveContext?.Palette.GetCategories()
            .FirstOrDefault(c => c.Blueprints.Contains(bp));

    // ---- helpers ------------------------------------------------------------

    private void RefreshChrome()
    {
        var vm = _host?.ActiveContext?.ViewModel;
        DirtyIndicator.IsVisible = vm?.IsDirty == true;

        if (_host?.ActiveContext is { } ctx)
        {
            int total = ctx.Store.ResRefs.Count;
            int drifted = ctx.Store.ResRefs.Count(r => ctx.ViewModel.Classify(r).Kind == PalettePlacementKind.Drifted);
            int uncat = ctx.ViewModel.GetUncategorized().Count();
            StatusText.Text = $"{total} blueprints, {drifted} drifted, {uncat} uncategorized";
        }
        else
        {
            StatusText.Text = string.Empty;
        }
    }

    // A failed save means nothing was written (the transaction is all-or-nothing). Surface it in the
    // status line rather than silently leaving the editor "unsaved" with no explanation.
    private void NotifySaveFailed()
    {
        StatusText.Text = "Save failed — no files were changed. See the log for details.";
    }
}
