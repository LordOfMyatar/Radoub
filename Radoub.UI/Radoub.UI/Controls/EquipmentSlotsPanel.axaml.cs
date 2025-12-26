using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Radoub.UI.ViewModels;

namespace Radoub.UI.Controls;

/// <summary>
/// Visual equipment slot display showing creature's equipped items.
/// Displays a grid of slots with item icons and validation warnings.
/// </summary>
public partial class EquipmentSlotsPanel : UserControl
{
    /// <summary>
    /// All equipment slots (standard + natural).
    /// </summary>
    public static readonly StyledProperty<ObservableCollection<EquipmentSlotViewModel>> SlotsProperty =
        AvaloniaProperty.Register<EquipmentSlotsPanel, ObservableCollection<EquipmentSlotViewModel>>(
            nameof(Slots),
            defaultValue: new ObservableCollection<EquipmentSlotViewModel>());

    /// <summary>
    /// Currently selected slot.
    /// </summary>
    public static readonly StyledProperty<EquipmentSlotViewModel?> SelectedSlotProperty =
        AvaloniaProperty.Register<EquipmentSlotsPanel, EquipmentSlotViewModel?>(nameof(SelectedSlot));

    /// <summary>
    /// Whether to show natural equipment slots (creature-only).
    /// </summary>
    public static readonly StyledProperty<bool> ShowNaturalSlotsProperty =
        AvaloniaProperty.Register<EquipmentSlotsPanel, bool>(nameof(ShowNaturalSlots), defaultValue: false);

    /// <summary>
    /// Event raised when a slot is clicked.
    /// </summary>
    public event EventHandler<EquipmentSlotViewModel>? SlotClicked;

    /// <summary>
    /// Event raised when a slot is double-clicked.
    /// </summary>
    public event EventHandler<EquipmentSlotViewModel>? SlotDoubleClicked;

    /// <summary>
    /// Event raised when drag operation starts from a slot.
    /// </summary>
    public event EventHandler<EquipmentSlotDragEventArgs>? DragStarting;

    /// <summary>
    /// Event raised when an item is dropped on a slot.
    /// </summary>
    public event EventHandler<EquipmentSlotDropEventArgs>? ItemDropped;

    // Slot controls mapped by slot ID
    private readonly Dictionary<int, EquipmentSlotControl> _slotControls = new();

    public EquipmentSlotsPanel()
    {
        InitializeComponent();

        // Wire up tab switching
        StandardTab.IsCheckedChanged += OnTabChanged;
        NaturalTab.IsCheckedChanged += OnTabChanged;

        Loaded += OnLoaded;
    }

    /// <summary>
    /// All equipment slots.
    /// </summary>
    public ObservableCollection<EquipmentSlotViewModel> Slots
    {
        get => GetValue(SlotsProperty);
        set => SetValue(SlotsProperty, value);
    }

    /// <summary>
    /// Currently selected slot.
    /// </summary>
    public EquipmentSlotViewModel? SelectedSlot
    {
        get => GetValue(SelectedSlotProperty);
        set => SetValue(SelectedSlotProperty, value);
    }

    /// <summary>
    /// Whether to show the natural slots tab.
    /// </summary>
    public bool ShowNaturalSlots
    {
        get => GetValue(ShowNaturalSlotsProperty);
        set => SetValue(ShowNaturalSlotsProperty, value);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Create slot controls if not yet created
        if (_slotControls.Count == 0)
        {
            CreateSlotControls();
        }

        UpdateSlotBindings();
        UpdateNaturalTabVisibility();
    }

    private void CreateSlotControls()
    {
        // Map slot IDs to their ContentControl placeholders
        var slotMappings = new Dictionary<int, ContentControl>
        {
            { 0, SlotHead },
            { 1, SlotChest },
            { 2, SlotBoots },
            { 3, SlotArms },
            { 4, SlotRightHand },
            { 5, SlotLeftHand },
            { 6, SlotCloak },
            { 7, SlotLeftRing },
            { 8, SlotRightRing },
            { 9, SlotNeck },
            { 10, SlotBelt },
            { 11, SlotArrows },
            { 12, SlotBullets },
            { 13, SlotBolts },
            { 14, SlotClaw1 },
            { 15, SlotClaw2 },
            { 16, SlotClaw3 },
            { 17, SlotSkin }
        };

        foreach (var (slotId, placeholder) in slotMappings)
        {
            var slotControl = new EquipmentSlotControl();
            slotControl.SlotClicked += OnSlotControlClicked;
            slotControl.SlotDoubleClicked += OnSlotControlDoubleClicked;
            slotControl.DragStarting += OnSlotDragStarting;
            slotControl.ItemDropped += OnSlotItemDropped;

            placeholder.Content = slotControl;
            _slotControls[slotId] = slotControl;
        }
    }

    private void UpdateSlotBindings()
    {
        foreach (var slot in Slots)
        {
            if (_slotControls.TryGetValue(slot.SlotId, out var control))
            {
                control.Slot = slot;
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SlotsProperty)
        {
            UpdateSlotBindings();
        }
        else if (change.Property == ShowNaturalSlotsProperty)
        {
            UpdateNaturalTabVisibility();
        }
        else if (change.Property == SelectedSlotProperty)
        {
            UpdateSlotSelection();
        }
    }

    private void UpdateNaturalTabVisibility()
    {
        NaturalTab.IsVisible = ShowNaturalSlots;
    }

    private void UpdateSlotSelection()
    {
        foreach (var slot in Slots)
        {
            slot.IsSelected = slot == SelectedSlot;
        }
    }

    private void OnTabChanged(object? sender, RoutedEventArgs e)
    {
        var showNatural = NaturalTab.IsChecked == true;
        StandardPanel.IsVisible = !showNatural;
        NaturalPanel.IsVisible = showNatural;
    }

    private void OnSlotControlClicked(object? sender, EquipmentSlotViewModel slot)
    {
        SelectedSlot = slot;
        SlotClicked?.Invoke(this, slot);
    }

    private void OnSlotControlDoubleClicked(object? sender, EquipmentSlotViewModel slot)
    {
        SlotDoubleClicked?.Invoke(this, slot);
    }

    private void OnSlotDragStarting(object? sender, EquipmentSlotDragEventArgs e)
    {
        DragStarting?.Invoke(this, e);
    }

    private void OnSlotItemDropped(object? sender, EquipmentSlotDropEventArgs e)
    {
        ItemDropped?.Invoke(this, e);
    }

    /// <summary>
    /// Clears all equipped items from slots.
    /// </summary>
    public void ClearAllSlots()
    {
        foreach (var slot in Slots)
        {
            slot.EquippedItem = null;
            slot.ValidationWarning = null;
        }
    }

    /// <summary>
    /// Gets a slot by its bit flag.
    /// </summary>
    public EquipmentSlotViewModel? GetSlotByFlag(int flag)
    {
        return Slots.FirstOrDefault(s => s.SlotFlag == flag);
    }

    /// <summary>
    /// Gets a slot by its ID.
    /// </summary>
    public EquipmentSlotViewModel? GetSlotById(int slotId)
    {
        return Slots.FirstOrDefault(s => s.SlotId == slotId);
    }
}

/// <summary>
/// Individual equipment slot control.
/// </summary>
public class EquipmentSlotControl : TemplatedControl
{
    /// <summary>
    /// The slot view model.
    /// </summary>
    public static readonly StyledProperty<EquipmentSlotViewModel?> SlotProperty =
        AvaloniaProperty.Register<EquipmentSlotControl, EquipmentSlotViewModel?>(nameof(Slot));

    /// <summary>
    /// Event raised when slot is clicked.
    /// </summary>
    public event EventHandler<EquipmentSlotViewModel>? SlotClicked;

    /// <summary>
    /// Event raised when slot is double-clicked.
    /// </summary>
    public event EventHandler<EquipmentSlotViewModel>? SlotDoubleClicked;

    /// <summary>
    /// Event raised when drag starts.
    /// </summary>
    public event EventHandler<EquipmentSlotDragEventArgs>? DragStarting;

    /// <summary>
    /// Event raised when item is dropped.
    /// </summary>
    public event EventHandler<EquipmentSlotDropEventArgs>? ItemDropped;

    // Drag state
    private Point _dragStartPoint;
    private bool _potentialDrag;
    private const double DragThreshold = 5;

    public EquipmentSlotControl()
    {
        // Enable drag-drop
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);

        // Handle double-tap via event
        DoubleTapped += OnDoubleTapped;
    }

    /// <summary>
    /// The slot view model.
    /// </summary>
    public EquipmentSlotViewModel? Slot
    {
        get => GetValue(SlotProperty);
        set => SetValue(SlotProperty, value);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Slot == null) return;

        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = e.GetPosition(this);
            _potentialDrag = Slot.HasItem;

            SlotClicked?.Invoke(this, Slot);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_potentialDrag || Slot?.EquippedItem == null) return;

        var currentPoint = e.GetPosition(this);
        var delta = currentPoint - _dragStartPoint;

        if (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold)
        {
            _potentialDrag = false;
            StartDrag(e);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _potentialDrag = false;
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Slot != null)
        {
            SlotDoubleClicked?.Invoke(this, Slot);
        }
    }

    private async void StartDrag(PointerEventArgs e)
    {
        if (Slot?.EquippedItem == null) return;

        var args = new EquipmentSlotDragEventArgs(Slot);
        DragStarting?.Invoke(this, args);

        if (args.Data != null)
        {
#pragma warning disable CS0618 // DataObject is obsolete - matches ItemListView pattern
            var dragData = new DataObject();
            dragData.Set(args.DataFormat ?? "EquipmentSlotItem", args.Data);
            await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move);
#pragma warning restore CS0618
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // Accept drops if this slot can receive items
        e.DragEffects = DragDropEffects.Move;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (Slot == null) return;

        var args = new EquipmentSlotDropEventArgs(Slot, e.DataTransfer);
        ItemDropped?.Invoke(this, args);
    }
}

/// <summary>
/// Event args for drag operations from equipment slots.
/// </summary>
public class EquipmentSlotDragEventArgs : EventArgs
{
    /// <summary>
    /// The slot being dragged from.
    /// </summary>
    public EquipmentSlotViewModel Slot { get; }

    /// <summary>
    /// Data to include in drag operation. Set by event handler.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Data format string.
    /// </summary>
    public string? DataFormat { get; set; }

    public EquipmentSlotDragEventArgs(EquipmentSlotViewModel slot)
    {
        Slot = slot;
    }
}

/// <summary>
/// Event args for drop operations on equipment slots.
/// </summary>
public class EquipmentSlotDropEventArgs : EventArgs
{
    /// <summary>
    /// The slot receiving the drop.
    /// </summary>
    public EquipmentSlotViewModel TargetSlot { get; }

    /// <summary>
    /// The drag data transfer object.
    /// </summary>
    public IDataTransfer DataTransfer { get; }

    public EquipmentSlotDropEventArgs(EquipmentSlotViewModel targetSlot, IDataTransfer dataTransfer)
    {
        TargetSlot = targetSlot;
        DataTransfer = dataTransfer;
    }
}
