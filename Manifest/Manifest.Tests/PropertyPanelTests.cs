using System.Collections.Generic;
using Manifest.Views;
using Radoub.Formats.Jrl;
using Xunit;

namespace Manifest.Tests;

/// <summary>
/// Tests for MainWindow.PropertyPanel pure logic methods.
/// </summary>
public class PropertyPanelTests
{
    #region FormatStrRef

    [Fact]
    public void FormatStrRef_InvalidStrRef_ReturnsNone()
    {
        var result = MainWindow.FormatStrRef(0xFFFFFFFF);

        Assert.Equal("(none)", result);
    }

    [Fact]
    public void FormatStrRef_Zero_ReturnsFormattedString()
    {
        var result = MainWindow.FormatStrRef(0);

        Assert.Equal("0 (0x00000000)", result);
    }

    [Fact]
    public void FormatStrRef_ValidStrRef_ReturnsDecimalAndHex()
    {
        var result = MainWindow.FormatStrRef(12443);

        Assert.Equal("12443 (0x0000309B)", result);
    }

    [Fact]
    public void FormatStrRef_LargeStrRef_FormatsCorrectly()
    {
        var result = MainWindow.FormatStrRef(0x00FFFFFF);

        Assert.Equal("16777215 (0x00FFFFFF)", result);
    }

    [Fact]
    public void FormatStrRef_MaxValidStrRef_FormatsCorrectly()
    {
        // Largest valid StrRef (one less than 0xFFFFFFFF)
        var result = MainWindow.FormatStrRef(0xFFFFFFFE);

        Assert.Equal("4294967294 (0xFFFFFFFE)", result);
    }

    #endregion

    #region IsEntryIdDuplicate (#2253)

    private static JournalCategory CategoryWithIds(params uint[] ids)
    {
        var cat = new JournalCategory();
        foreach (var id in ids)
            cat.Entries.Add(new JournalEntry { ID = id });
        return cat;
    }

    [Fact]
    public void IsEntryIdDuplicate_UniqueId_ReturnsFalse()
    {
        var cat = CategoryWithIds(100, 200, 300);
        var moving = cat.Entries[0]; // ID 100

        Assert.False(MainWindow.IsEntryIdDuplicate(cat, moving, 150));
    }

    [Fact]
    public void IsEntryIdDuplicate_CollidesWithSibling_ReturnsTrue()
    {
        var cat = CategoryWithIds(100, 200, 300);
        var moving = cat.Entries[0]; // ID 100

        Assert.True(MainWindow.IsEntryIdDuplicate(cat, moving, 200));
    }

    [Fact]
    public void IsEntryIdDuplicate_SameAsOwnCurrentId_ReturnsFalse()
    {
        // The entry being edited must not count itself as a duplicate.
        var cat = CategoryWithIds(100, 200, 300);
        var moving = cat.Entries[1]; // ID 200

        Assert.False(MainWindow.IsEntryIdDuplicate(cat, moving, 200));
    }

    [Fact]
    public void IsEntryIdDuplicate_NullCategory_ReturnsFalse()
    {
        Assert.False(MainWindow.IsEntryIdDuplicate(null, new JournalEntry { ID = 5 }, 5));
    }

    #endregion
}
