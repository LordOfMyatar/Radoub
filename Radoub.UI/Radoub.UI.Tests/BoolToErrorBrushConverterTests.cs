using Avalonia.Media;
using Radoub.UI.Converters;
using Xunit;

namespace Radoub.UI.Tests;

public class BoolToErrorBrushConverterTests
{
    private readonly BoolToErrorBrushConverter _converter = new();

    [Fact]
    public void Convert_True_ReturnsNonTransparentBrush()
    {
        var result = _converter.Convert(true, typeof(IBrush), null, null!);
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IBrush>(result);
        Assert.NotEqual(Brushes.Transparent, result);
    }

    [Fact]
    public void Convert_False_ReturnsTransparent()
    {
        var result = _converter.Convert(false, typeof(IBrush), null, null!);
        Assert.Equal(Brushes.Transparent, result);
    }

    [Fact]
    public void Convert_Null_ReturnsTransparent()
    {
        var result = _converter.Convert(null, typeof(IBrush), null, null!);
        Assert.Equal(Brushes.Transparent, result);
    }

    [Fact]
    public void Convert_NonBool_ReturnsTransparent()
    {
        var result = _converter.Convert("not a bool", typeof(IBrush), null, null!);
        Assert.Equal(Brushes.Transparent, result);
    }
}
