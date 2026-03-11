using Radoub.Formats.Uti;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using Radoub.UI.ViewModels;
using Xunit;

namespace Radoub.UI.Tests;

public class EquipmentSlotValidatorTests
{
    [Fact]
    public void ValidateSlot_EmptySlot_ReturnsNull()
    {
        // Arrange
        var mockGameData = new MockGameDataService(includeSampleData: false);
        var validator = new EquipmentSlotValidator(mockGameData);
        var slot = new EquipmentSlotViewModel(0, 0x1, "Head");

        // Act
        var result = validator.ValidateSlot(slot);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ValidateSlot_ValidEquipment_ReturnsNull()
    {
        // Arrange
        var mockGameData = new MockGameDataService(includeSampleData: false);
        // Helmet base item (80) can be equipped in HEAD (0x1)
        mockGameData.Set2DAValue("baseitems", 80, "EquipableSlots", "1");

        var validator = new EquipmentSlotValidator(mockGameData);
        var slot = new EquipmentSlotViewModel(0, 0x1, "Head");

        var item = new UtiFile { BaseItem = 80 };
        slot.EquippedItem = new ItemViewModel(item, "Test Helm", "Helmet", "");

        // Act
        var result = validator.ValidateSlot(slot);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ValidateSlot_InvalidEquipment_ReturnsWarning()
    {
        // Arrange
        var mockGameData = new MockGameDataService(includeSampleData: false);
        // Sword (1) can only be equipped in RIGHTHAND (0x10) or LEFTHAND (0x20)
        mockGameData.Set2DAValue("baseitems", 1, "EquipableSlots", "48"); // 0x30 = 0x10 | 0x20

        var validator = new EquipmentSlotValidator(mockGameData);
        var slot = new EquipmentSlotViewModel(0, 0x1, "Head"); // HEAD slot

        var item = new UtiFile { BaseItem = 1 };
        slot.EquippedItem = new ItemViewModel(item, "Test Sword", "Longsword", "");

        // Act
        var result = validator.ValidateSlot(slot);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Longsword", result);
        Assert.Contains("Head", result);
    }

    [Fact]
    public void ValidateSlot_No2DAData_ReturnsNull()
    {
        // Arrange
        var mockGameData = new MockGameDataService(includeSampleData: false);
        // No 2DA data set

        var validator = new EquipmentSlotValidator(mockGameData);
        var slot = new EquipmentSlotViewModel(0, 0x1, "Head");

        var item = new UtiFile { BaseItem = 999 };
        slot.EquippedItem = new ItemViewModel(item, "Unknown", "Unknown", "");

        // Act
        var result = validator.ValidateSlot(slot);

        // Assert - should not warn if we can't validate
        Assert.Null(result);
    }

    [Fact]
    public void CanEquipInSlot_ValidSlot_ReturnsTrue()
    {
        // Arrange
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 1, "EquipableSlots", "48"); // RIGHTHAND | LEFTHAND

        var validator = new EquipmentSlotValidator(mockGameData);

        // Act
        var canEquipRightHand = validator.CanEquipInSlot(1, 0x10);
        var canEquipLeftHand = validator.CanEquipInSlot(1, 0x20);

        // Assert
        Assert.True(canEquipRightHand);
        Assert.True(canEquipLeftHand);
    }

    [Fact]
    public void CanEquipInSlot_InvalidSlot_ReturnsFalse()
    {
        // Arrange
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 1, "EquipableSlots", "48"); // RIGHTHAND | LEFTHAND

        var validator = new EquipmentSlotValidator(mockGameData);

        // Act
        var canEquipHead = validator.CanEquipInSlot(1, 0x1);

        // Assert
        Assert.False(canEquipHead);
    }

    [Fact]
    public void CanEquipInSlot_NoData_ReturnsTrue()
    {
        // Arrange
        var mockGameData = new MockGameDataService(includeSampleData: false);
        var validator = new EquipmentSlotValidator(mockGameData);

        // Act - no data means we allow (can't validate)
        var result = validator.CanEquipInSlot(999, 0x1);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetEquipableSlots_ReturnsCorrectValue()
    {
        // Arrange
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 80, "EquipableSlots", "1"); // HEAD only

        var validator = new EquipmentSlotValidator(mockGameData);

        // Act
        var slots = validator.GetEquipableSlots(80);

        // Assert
        Assert.Equal(1, slots);
    }

    [Fact]
    public void GetEquipableSlots_HandlesHexFormat()
    {
        // Arrange
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 1, "EquipableSlots", "0x30");

        var validator = new EquipmentSlotValidator(mockGameData);

        // Act
        var slots = validator.GetEquipableSlots(1);

        // Assert
        Assert.Equal(0x30, slots);
    }

    [Fact]
    public void GetEquipableSlots_HandlesStarValue()
    {
        // Arrange
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 1, "EquipableSlots", "****");

        var validator = new EquipmentSlotValidator(mockGameData);

        // Act
        var slots = validator.GetEquipableSlots(1);

        // Assert
        Assert.Null(slots);
    }

    [Fact]
    public void GetValidSlotNames_ReturnsCorrectNames()
    {
        // Arrange
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 1, "EquipableSlots", "48"); // RIGHTHAND | LEFTHAND

        var validator = new EquipmentSlotValidator(mockGameData);

        // Act
        var names = validator.GetValidSlotNames(1);

        // Assert
        Assert.Equal(2, names.Count);
        Assert.Contains("Right Hand", names);
        Assert.Contains("Left Hand", names);
    }

    [Fact]
    public void GetValidSlotNames_NoData_ReturnsEmpty()
    {
        // Arrange
        var mockGameData = new MockGameDataService(includeSampleData: false);
        var validator = new EquipmentSlotValidator(mockGameData);

        // Act
        var names = validator.GetValidSlotNames(999);

        // Assert
        Assert.Empty(names);
    }

    #region Creature Compatibility - Weapon Size

    [Fact]
    public void ValidateCreatureCompatibility_MediumCreature_MediumWeapon_NoWarning()
    {
        // Arrange: Medium creature (size 3) with a longsword (WeaponSize 3)
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 1, "EquipableSlots", "0x10"); // Right hand
        mockGameData.Set2DAValue("baseitems", 1, "WeaponSize", "3"); // Medium weapon

        var validator = new EquipmentSlotValidator(mockGameData);
        var slot = new EquipmentSlotViewModel(4, 0x10, "Right Hand");

        var item = new UtiFile { BaseItem = 1 };
        slot.EquippedItem = new ItemViewModel(item, "Longsword", "Longsword", "");

        // Act
        var warning = validator.ValidateCreatureCompatibility(slot, creatureSize: 3);

        // Assert
        Assert.Null(warning);
    }

    [Fact]
    public void ValidateCreatureCompatibility_SmallCreature_LargeWeapon_ReturnsWarning()
    {
        // Arrange: Small creature (size 2) with a greatsword (WeaponSize 4 = Large)
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 2, "EquipableSlots", "0x10");
        mockGameData.Set2DAValue("baseitems", 2, "WeaponSize", "4"); // Large weapon

        var validator = new EquipmentSlotValidator(mockGameData);
        var slot = new EquipmentSlotViewModel(4, 0x10, "Right Hand");

        var item = new UtiFile { BaseItem = 2 };
        slot.EquippedItem = new ItemViewModel(item, "Greatsword", "Greatsword", "");

        // Act
        var warning = validator.ValidateCreatureCompatibility(slot, creatureSize: 2);

        // Assert
        Assert.NotNull(warning);
        Assert.Contains("too large", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateCreatureCompatibility_MediumCreature_LargeWeapon_NoWarning()
    {
        // Arrange: Medium creature (size 3) with a Large weapon (size 4)
        // In NWN, medium creatures can wield large weapons two-handed — allowed
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 2, "EquipableSlots", "0x10");
        mockGameData.Set2DAValue("baseitems", 2, "WeaponSize", "4");

        var validator = new EquipmentSlotValidator(mockGameData);
        var slot = new EquipmentSlotViewModel(4, 0x10, "Right Hand");

        var item = new UtiFile { BaseItem = 2 };
        slot.EquippedItem = new ItemViewModel(item, "Greatsword", "Greatsword", "");

        // Act
        var warning = validator.ValidateCreatureCompatibility(slot, creatureSize: 3);

        // Assert
        Assert.Null(warning);
    }

    [Fact]
    public void ValidateCreatureCompatibility_TinyCreature_MediumWeapon_ReturnsWarning()
    {
        // Arrange: Tiny creature (size 1) with a Medium weapon (size 3) — 2 sizes too large
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 1, "EquipableSlots", "0x10");
        mockGameData.Set2DAValue("baseitems", 1, "WeaponSize", "3");

        var validator = new EquipmentSlotValidator(mockGameData);
        var slot = new EquipmentSlotViewModel(4, 0x10, "Right Hand");

        var item = new UtiFile { BaseItem = 1 };
        slot.EquippedItem = new ItemViewModel(item, "Longsword", "Longsword", "");

        // Act
        var warning = validator.ValidateCreatureCompatibility(slot, creatureSize: 1);

        // Assert
        Assert.NotNull(warning);
        Assert.Contains("too large", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateCreatureCompatibility_NonWeaponSlot_NoWarning()
    {
        // Arrange: Helmet in head slot — no weapon size check needed
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 80, "EquipableSlots", "0x1"); // Head
        // No WeaponSize for helmet

        var validator = new EquipmentSlotValidator(mockGameData);
        var slot = new EquipmentSlotViewModel(0, 0x1, "Head");

        var item = new UtiFile { BaseItem = 80 };
        slot.EquippedItem = new ItemViewModel(item, "Helmet", "Helmet", "");

        // Act
        var warning = validator.ValidateCreatureCompatibility(slot, creatureSize: 2);

        // Assert
        Assert.Null(warning);
    }

    [Fact]
    public void ValidateCreatureCompatibility_EmptySlot_NoWarning()
    {
        var mockGameData = new MockGameDataService(includeSampleData: false);
        var validator = new EquipmentSlotValidator(mockGameData);
        var slot = new EquipmentSlotViewModel(4, 0x10, "Right Hand");

        var warning = validator.ValidateCreatureCompatibility(slot, creatureSize: 3);

        Assert.Null(warning);
    }

    [Fact]
    public void ValidateCreatureCompatibility_NoWeaponSizeData_NoWarning()
    {
        // Arrange: WeaponSize column missing/empty — can't validate, allow
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 1, "EquipableSlots", "0x10");
        // No WeaponSize set

        var validator = new EquipmentSlotValidator(mockGameData);
        var slot = new EquipmentSlotViewModel(4, 0x10, "Right Hand");

        var item = new UtiFile { BaseItem = 1 };
        slot.EquippedItem = new ItemViewModel(item, "Sword", "Longsword", "");

        var warning = validator.ValidateCreatureCompatibility(slot, creatureSize: 2);

        Assert.Null(warning);
    }

    [Fact]
    public void ValidateAllCreatureCompatibility_SetsWarningsOnOversizedWeapons()
    {
        // Arrange: Small creature with a large weapon in right hand, normal helmet in head
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 2, "EquipableSlots", "0x10");
        mockGameData.Set2DAValue("baseitems", 2, "WeaponSize", "4"); // Large
        mockGameData.Set2DAValue("baseitems", 80, "EquipableSlots", "0x1");
        // No WeaponSize for helmet

        var validator = new EquipmentSlotValidator(mockGameData);

        var headSlot = new EquipmentSlotViewModel(0, 0x1, "Head");
        var rightSlot = new EquipmentSlotViewModel(4, 0x10, "Right Hand");

        var helm = new UtiFile { BaseItem = 80 };
        headSlot.EquippedItem = new ItemViewModel(helm, "Helmet", "Helmet", "");

        var sword = new UtiFile { BaseItem = 2 };
        rightSlot.EquippedItem = new ItemViewModel(sword, "Greatsword", "Greatsword", "");

        // Act
        validator.ValidateAllCreatureCompatibility(new[] { headSlot, rightSlot }, creatureSize: 2);

        // Assert
        Assert.Null(headSlot.ValidationWarning);
        Assert.NotNull(rightSlot.ValidationWarning);
    }

    #endregion

    [Fact]
    public void ValidateAllSlots_SetsWarningsOnAllSlots()
    {
        // Arrange
        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 1, "EquipableSlots", "48"); // RIGHTHAND | LEFTHAND only
        mockGameData.Set2DAValue("baseitems", 80, "EquipableSlots", "1"); // HEAD only

        var validator = new EquipmentSlotValidator(mockGameData);

        var headSlot = new EquipmentSlotViewModel(0, 0x1, "Head");
        var rightHandSlot = new EquipmentSlotViewModel(4, 0x10, "Right Hand");

        // Sword in head (invalid)
        var sword = new UtiFile { BaseItem = 1 };
        headSlot.EquippedItem = new ItemViewModel(sword, "Sword", "Longsword", "");

        // Sword in right hand (valid)
        rightHandSlot.EquippedItem = new ItemViewModel(sword, "Sword", "Longsword", "");

        // Act
        validator.ValidateAllSlots(new[] { headSlot, rightHandSlot });

        // Assert
        Assert.NotNull(headSlot.ValidationWarning);
        Assert.Null(rightHandSlot.ValidationWarning);
    }
}
