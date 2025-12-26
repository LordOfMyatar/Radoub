using Radoub.Formats.Services;
using Radoub.Formats.TwoDA;
using Radoub.Formats.Uti;
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
        var mockGameData = new MockGameDataService();
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
        var mockGameData = new MockGameDataService();
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
        var mockGameData = new MockGameDataService();
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
        var mockGameData = new MockGameDataService();
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
        var mockGameData = new MockGameDataService();
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
        var mockGameData = new MockGameDataService();
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
        var mockGameData = new MockGameDataService();
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
        var mockGameData = new MockGameDataService();
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
        var mockGameData = new MockGameDataService();
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
        var mockGameData = new MockGameDataService();
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
        var mockGameData = new MockGameDataService();
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
        var mockGameData = new MockGameDataService();
        var validator = new EquipmentSlotValidator(mockGameData);

        // Act
        var names = validator.GetValidSlotNames(999);

        // Assert
        Assert.Empty(names);
    }

    [Fact]
    public void ValidateAllSlots_SetsWarningsOnAllSlots()
    {
        // Arrange
        var mockGameData = new MockGameDataService();
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

    /// <summary>
    /// Mock game data service for testing without file system access.
    /// </summary>
    private class MockGameDataService : IGameDataService
    {
        private readonly Dictionary<(string twoDA, int row, string col), string> _2daValues = new();
        private readonly Dictionary<uint, string> _tlkStrings = new();

        public void Set2DAValue(string twoDA, int row, string col, string value)
        {
            _2daValues[(twoDA.ToLowerInvariant(), row, col.ToLowerInvariant())] = value;
        }

        public void SetTlkString(uint strRef, string value)
        {
            _tlkStrings[strRef] = value;
        }

        public TwoDAFile? Get2DA(string name) => null;

        public string? Get2DAValue(string twoDAName, int rowIndex, string columnName)
        {
            var key = (twoDAName.ToLowerInvariant(), rowIndex, columnName.ToLowerInvariant());
            return _2daValues.TryGetValue(key, out var value) ? value : null;
        }

        public bool Has2DA(string name) => false;
        public void ClearCache() { }

        public string? GetString(uint strRef)
        {
            return _tlkStrings.TryGetValue(strRef, out var value) ? value : null;
        }

        public string? GetString(string? strRefStr)
        {
            if (string.IsNullOrEmpty(strRefStr) || strRefStr == "****")
                return null;
            if (uint.TryParse(strRefStr, out uint strRef))
                return GetString(strRef);
            return null;
        }

        public bool HasCustomTlk => false;
        public void SetCustomTlk(string? path) { }
        public byte[]? FindResource(string resRef, ushort resourceType) => null;
        public IEnumerable<GameResourceInfo> ListResources(ushort resourceType) => Array.Empty<GameResourceInfo>();
        public bool IsConfigured => true;
        public void ReloadConfiguration() { }
        public void Dispose() { }
    }
}
