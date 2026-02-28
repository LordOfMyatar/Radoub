using Manifest.Views;
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
}
