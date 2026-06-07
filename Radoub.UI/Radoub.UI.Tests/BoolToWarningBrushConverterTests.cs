using Avalonia;
using Avalonia.Media;
using Radoub.UI.Converters;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// #2182: status-text warning color. True → a concrete warning brush; non-warning
/// → no override (UnsetValue or a resolved default), never the warning brush.
/// </summary>
public class BoolToWarningBrushConverterTests
{
    private readonly BoolToWarningBrushConverter _converter = new();

    [Fact]
    public void Convert_True_ReturnsBrush()
    {
        var result = _converter.Convert(true, typeof(IBrush), null, null!);
        Assert.IsAssignableFrom<IBrush>(result);
    }

    [Fact]
    public void Convert_False_DoesNotReturnWarningBrush()
    {
        // No app/theme in the test host → resource lookup misses → UnsetValue.
        // The key guarantee: false never yields a warning brush.
        var warning = _converter.Convert(true, typeof(IBrush), null, null!);
        var notWarning = _converter.Convert(false, typeof(IBrush), null, null!);
        Assert.NotEqual(warning, notWarning);
    }

    [Fact]
    public void Convert_NonBool_DoesNotReturnWarningBrush()
    {
        var result = _converter.Convert("not a bool", typeof(IBrush), null, null!);
        Assert.Equal(AvaloniaProperty.UnsetValue, result);
    }
}
