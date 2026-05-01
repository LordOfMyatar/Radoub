using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Radoub.UI.Converters;

/// <summary>
/// Returns the bound string when non-empty, otherwise an em-dash ("—").
/// Used by ItemDetailsPanel to render placeholder dashes for missing tags
/// or source locations, matching Fence's pre-extraction behavior.
/// </summary>
public sealed class StringOrEmDashConverter : IValueConverter
{
    public static readonly StringOrEmDashConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;
        return string.IsNullOrWhiteSpace(s) ? "—" : s;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
