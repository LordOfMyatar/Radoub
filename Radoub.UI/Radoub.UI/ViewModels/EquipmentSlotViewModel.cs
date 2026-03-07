using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.UI.Services;

namespace Radoub.UI.ViewModels;

/// <summary>
/// Visual size categories for equipment slot rendering in the paperdoll layout.
/// Base grid unit is 32px. Sizes are expressed as grid multiples (WxH).
/// </summary>
public enum SlotSize
{
    /// <summary>1x1 grid unit (rings, amulet, bullets). 32x32.</summary>
    Small,
    /// <summary>2x1 grid units (belt). 64x32.</summary>
    Wide,
    /// <summary>2x2 grid units (head, boots, gloves). 64x64.</summary>
    Medium,
    /// <summary>2x3 grid units (armor, cloak, left hand). 64x96.</summary>
    Large,
    /// <summary>1x3 grid units (arrows, bolts). 32x96.</summary>
    Tall,
    /// <summary>2x4 grid units (right hand). 64x128.</summary>
    ExtraTall
}

/// <summary>
/// ViewModel for a single equipment slot in the EquipmentSlotsPanel.
/// </summary>
public partial class EquipmentSlotViewModel : ObservableObject
{
    /// <summary>
    /// Creates an equipment slot view model.
    /// </summary>
    /// <param name="slotId">Slot ID (0-17).</param>
    /// <param name="slotFlag">Bit flag for GFF struct ID.</param>
    /// <param name="name">Display name.</param>
    /// <param name="isNatural">True if this is a creature-only natural slot.</param>
    public EquipmentSlotViewModel(int slotId, int slotFlag, string name, bool isNatural = false)
    {
        SlotId = slotId;
        SlotFlag = slotFlag;
        Name = name;
        IsNatural = isNatural;
    }

    /// <summary>
    /// Slot ID (0-17 per issue spec).
    /// </summary>
    public int SlotId { get; }

    /// <summary>
    /// Bit flag value used in GFF Equip_ItemList struct ID.
    /// </summary>
    public int SlotFlag { get; }

    /// <summary>
    /// Display name (e.g., "Head", "Chest", "Claw 1").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// True if this is a natural equipment slot (creature-only: Claws, Skin).
    /// </summary>
    public bool IsNatural { get; }

    /// <summary>
    /// Path to the slot's placeholder icon resource.
    /// </summary>
    public string IconPath => ItemIconHelper.GetSlotIconPath(SlotFlag);

    /// <summary>
    /// Visual size category for paperdoll layout.
    /// </summary>
    public SlotSize Size { get; init; } = SlotSize.Medium;

    /// <summary>
    /// True if this is a standard equipment slot (Head, Chest, etc.).
    /// </summary>
    public bool IsStandard => !IsNatural;

    /// <summary>
    /// The item currently equipped in this slot, if any.
    /// </summary>
    [ObservableProperty]
    private ItemViewModel? _equippedItem;

    /// <summary>
    /// True if no item is equipped in this slot.
    /// </summary>
    public bool IsEmpty => EquippedItem == null;

    /// <summary>
    /// True if an item is equipped in this slot.
    /// </summary>
    public bool HasItem => EquippedItem != null;

    /// <summary>
    /// True if slot is selected (for context operations).
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Validation warning message, if any.
    /// </summary>
    [ObservableProperty]
    private string? _validationWarning;

    /// <summary>
    /// True if there's a validation warning for this slot.
    /// </summary>
    public bool HasWarning => !string.IsNullOrEmpty(ValidationWarning);

    /// <summary>
    /// Tooltip text combining item info and any warnings.
    /// </summary>
    public string Tooltip
    {
        get
        {
            if (IsEmpty)
                return $"{Name} (Empty)";

            var text = EquippedItem!.Name;
            if (HasWarning)
                text += $"\n{ValidationWarning}";

            return text;
        }
    }

    partial void OnEquippedItemChanged(ItemViewModel? value)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasItem));
        OnPropertyChanged(nameof(Tooltip));
    }

    partial void OnValidationWarningChanged(string? value)
    {
        OnPropertyChanged(nameof(HasWarning));
        OnPropertyChanged(nameof(Tooltip));
    }
}

/// <summary>
/// Factory for creating equipment slot view models with correct metadata.
/// </summary>
public static class EquipmentSlotFactory
{
    // Slot flags matching EquipmentSlots constants
    private const int FlagHead = 0x1;
    private const int FlagChest = 0x2;
    private const int FlagBoots = 0x4;
    private const int FlagArms = 0x8;
    private const int FlagRightHand = 0x10;
    private const int FlagLeftHand = 0x20;
    private const int FlagCloak = 0x40;
    private const int FlagLeftRing = 0x80;
    private const int FlagRightRing = 0x100;
    private const int FlagNeck = 0x200;
    private const int FlagBelt = 0x400;
    private const int FlagArrows = 0x800;
    private const int FlagBullets = 0x1000;
    private const int FlagBolts = 0x2000;
    private const int FlagClaw1 = 0x4000;
    private const int FlagClaw2 = 0x8000;
    private const int FlagClaw3 = 0x10000;
    private const int FlagSkin = 0x20000;

    /// <summary>
    /// Creates all standard equipment slots (14 slots) with paperdoll sizes.
    /// </summary>
    public static IReadOnlyList<EquipmentSlotViewModel> CreateStandardSlots()
    {
        return new List<EquipmentSlotViewModel>
        {
            new(0, FlagHead, "Head") { Size = SlotSize.Medium },
            new(1, FlagChest, "Chest") { Size = SlotSize.Large },
            new(2, FlagBoots, "Boots") { Size = SlotSize.Medium },
            new(3, FlagArms, "Arms") { Size = SlotSize.Medium },
            new(4, FlagRightHand, "Right Hand") { Size = SlotSize.ExtraTall },
            new(5, FlagLeftHand, "Left Hand") { Size = SlotSize.Large },
            new(6, FlagCloak, "Cloak") { Size = SlotSize.Large },
            new(7, FlagLeftRing, "Left Ring") { Size = SlotSize.Small },
            new(8, FlagRightRing, "Right Ring") { Size = SlotSize.Small },
            new(9, FlagNeck, "Neck") { Size = SlotSize.Small },
            new(10, FlagBelt, "Belt") { Size = SlotSize.Wide },
            new(11, FlagArrows, "Arrows") { Size = SlotSize.Tall },
            new(12, FlagBullets, "Bullets") { Size = SlotSize.Small },
            new(13, FlagBolts, "Bolts") { Size = SlotSize.Tall }
        };
    }

    /// <summary>
    /// Creates all natural equipment slots (4 creature-only slots).
    /// </summary>
    public static IReadOnlyList<EquipmentSlotViewModel> CreateNaturalSlots()
    {
        return new List<EquipmentSlotViewModel>
        {
            new(14, FlagClaw1, "Claw 1", isNatural: true) { Size = SlotSize.Tall },
            new(15, FlagClaw2, "Claw 2", isNatural: true) { Size = SlotSize.Tall },
            new(16, FlagClaw3, "Claw 3", isNatural: true) { Size = SlotSize.Tall },
            new(17, FlagSkin, "Skin", isNatural: true) { Size = SlotSize.Large }
        };
    }

    /// <summary>
    /// Creates all equipment slots (standard + natural).
    /// </summary>
    public static IReadOnlyList<EquipmentSlotViewModel> CreateAllSlots()
    {
        var all = new List<EquipmentSlotViewModel>();
        all.AddRange(CreateStandardSlots());
        all.AddRange(CreateNaturalSlots());
        return all;
    }

    /// <summary>
    /// Gets a slot by its bit flag value.
    /// </summary>
    public static EquipmentSlotViewModel? GetSlotByFlag(IEnumerable<EquipmentSlotViewModel> slots, int flag)
    {
        return slots.FirstOrDefault(s => s.SlotFlag == flag);
    }
}
