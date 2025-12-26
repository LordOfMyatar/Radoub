using Radoub.Formats.Uti;
using Radoub.UI.ViewModels;
using Xunit;

namespace Radoub.UI.Tests;

public class EquipmentSlotViewModelTests
{
    [Fact]
    public void Constructor_SetsBasicProperties()
    {
        // Act
        var slot = new EquipmentSlotViewModel(0, 0x1, "Head");

        // Assert
        Assert.Equal(0, slot.SlotId);
        Assert.Equal(0x1, slot.SlotFlag);
        Assert.Equal("Head", slot.Name);
        Assert.False(slot.IsNatural);
        Assert.True(slot.IsStandard);
    }

    [Fact]
    public void Constructor_NaturalSlot_SetsIsNatural()
    {
        // Act
        var slot = new EquipmentSlotViewModel(14, 0x4000, "Claw 1", isNatural: true);

        // Assert
        Assert.True(slot.IsNatural);
        Assert.False(slot.IsStandard);
    }

    [Fact]
    public void IsEmpty_WhenNoItem_ReturnsTrue()
    {
        var slot = new EquipmentSlotViewModel(0, 0x1, "Head");

        Assert.True(slot.IsEmpty);
        Assert.False(slot.HasItem);
    }

    [Fact]
    public void HasItem_WhenItemEquipped_ReturnsTrue()
    {
        var slot = new EquipmentSlotViewModel(0, 0x1, "Head");
        var item = new UtiFile { TemplateResRef = "test_helm" };
        slot.EquippedItem = new ItemViewModel(item, "Test Helm", "Helmet", "");

        Assert.False(slot.IsEmpty);
        Assert.True(slot.HasItem);
    }

    [Fact]
    public void ValidationWarning_WhenSet_UpdatesHasWarning()
    {
        var slot = new EquipmentSlotViewModel(0, 0x1, "Head");

        Assert.False(slot.HasWarning);

        slot.ValidationWarning = "Invalid equipment";

        Assert.True(slot.HasWarning);
    }

    [Fact]
    public void Tooltip_WhenEmpty_ShowsSlotName()
    {
        var slot = new EquipmentSlotViewModel(0, 0x1, "Head");

        Assert.Equal("Head (Empty)", slot.Tooltip);
    }

    [Fact]
    public void Tooltip_WhenItemEquipped_ShowsItemName()
    {
        var slot = new EquipmentSlotViewModel(0, 0x1, "Head");
        var item = new UtiFile { TemplateResRef = "test_helm" };
        slot.EquippedItem = new ItemViewModel(item, "Test Helm", "Helmet", "");

        Assert.Equal("Test Helm", slot.Tooltip);
    }

    [Fact]
    public void Tooltip_WithWarning_ShowsBoth()
    {
        var slot = new EquipmentSlotViewModel(0, 0x1, "Head");
        var item = new UtiFile { TemplateResRef = "test_sword" };
        slot.EquippedItem = new ItemViewModel(item, "Test Sword", "Longsword", "");
        slot.ValidationWarning = "Cannot equip in Head";

        Assert.Contains("Test Sword", slot.Tooltip);
        Assert.Contains("Cannot equip in Head", slot.Tooltip);
    }

    [Fact]
    public void IsSelected_WhenChanged_RaisesPropertyChanged()
    {
        var slot = new EquipmentSlotViewModel(0, 0x1, "Head");
        var propertyChanged = false;

        slot.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EquipmentSlotViewModel.IsSelected))
                propertyChanged = true;
        };

        slot.IsSelected = true;

        Assert.True(propertyChanged);
        Assert.True(slot.IsSelected);
    }
}

public class EquipmentSlotFactoryTests
{
    [Fact]
    public void CreateStandardSlots_Returns14Slots()
    {
        var slots = EquipmentSlotFactory.CreateStandardSlots();

        Assert.Equal(14, slots.Count);
    }

    [Fact]
    public void CreateStandardSlots_AllAreNotNatural()
    {
        var slots = EquipmentSlotFactory.CreateStandardSlots();

        Assert.All(slots, slot => Assert.False(slot.IsNatural));
    }

    [Fact]
    public void CreateStandardSlots_HasCorrectSlotIds()
    {
        var slots = EquipmentSlotFactory.CreateStandardSlots();

        for (int i = 0; i < 14; i++)
        {
            Assert.Equal(i, slots[i].SlotId);
        }
    }

    [Fact]
    public void CreateStandardSlots_HasCorrectSlotFlags()
    {
        var slots = EquipmentSlotFactory.CreateStandardSlots();

        Assert.Equal(0x1, slots[0].SlotFlag);   // Head
        Assert.Equal(0x2, slots[1].SlotFlag);   // Chest
        Assert.Equal(0x4, slots[2].SlotFlag);   // Boots
        Assert.Equal(0x8, slots[3].SlotFlag);   // Arms
        Assert.Equal(0x10, slots[4].SlotFlag);  // Right Hand
        Assert.Equal(0x20, slots[5].SlotFlag);  // Left Hand
        Assert.Equal(0x40, slots[6].SlotFlag);  // Cloak
        Assert.Equal(0x80, slots[7].SlotFlag);  // Left Ring
        Assert.Equal(0x100, slots[8].SlotFlag); // Right Ring
        Assert.Equal(0x200, slots[9].SlotFlag); // Neck
        Assert.Equal(0x400, slots[10].SlotFlag); // Belt
        Assert.Equal(0x800, slots[11].SlotFlag); // Arrows
        Assert.Equal(0x1000, slots[12].SlotFlag); // Bullets
        Assert.Equal(0x2000, slots[13].SlotFlag); // Bolts
    }

    [Fact]
    public void CreateNaturalSlots_Returns4Slots()
    {
        var slots = EquipmentSlotFactory.CreateNaturalSlots();

        Assert.Equal(4, slots.Count);
    }

    [Fact]
    public void CreateNaturalSlots_AllAreNatural()
    {
        var slots = EquipmentSlotFactory.CreateNaturalSlots();

        Assert.All(slots, slot => Assert.True(slot.IsNatural));
    }

    [Fact]
    public void CreateNaturalSlots_HasCorrectSlotIds()
    {
        var slots = EquipmentSlotFactory.CreateNaturalSlots();

        Assert.Equal(14, slots[0].SlotId); // Claw 1
        Assert.Equal(15, slots[1].SlotId); // Claw 2
        Assert.Equal(16, slots[2].SlotId); // Claw 3
        Assert.Equal(17, slots[3].SlotId); // Skin
    }

    [Fact]
    public void CreateAllSlots_Returns18Slots()
    {
        var slots = EquipmentSlotFactory.CreateAllSlots();

        Assert.Equal(18, slots.Count);
    }

    [Fact]
    public void GetSlotByFlag_FindsCorrectSlot()
    {
        var slots = EquipmentSlotFactory.CreateAllSlots();

        var headSlot = EquipmentSlotFactory.GetSlotByFlag(slots, 0x1);
        var chestSlot = EquipmentSlotFactory.GetSlotByFlag(slots, 0x2);

        Assert.NotNull(headSlot);
        Assert.Equal("Head", headSlot!.Name);

        Assert.NotNull(chestSlot);
        Assert.Equal("Chest", chestSlot!.Name);
    }

    [Fact]
    public void GetSlotByFlag_ReturnsNullForInvalidFlag()
    {
        var slots = EquipmentSlotFactory.CreateAllSlots();

        var slot = EquipmentSlotFactory.GetSlotByFlag(slots, 0x999999);

        Assert.Null(slot);
    }
}
