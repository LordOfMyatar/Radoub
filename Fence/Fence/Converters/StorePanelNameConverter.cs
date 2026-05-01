using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Radoub.Formats.Utm;

namespace MerchantEditor.Converters;

/// <summary>
/// Converts a UTM store-panel ID (0–4) to its human-readable name via
/// <see cref="StorePanels.GetPanelName"/>. Used by StoreItemExtrasPanel.
/// </summary>
public sealed class StorePanelNameConverter : IValueConverter
{
    public static readonly StorePanelNameConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int panelId)
            return StorePanels.GetPanelName(panelId);
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
