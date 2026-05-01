using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Radoub.UI.ViewModels;

namespace Radoub.UI.Converters;

/// <summary>
/// Converts an <see cref="ItemViewModel"/> into a display string for its item properties.
/// Returns <see cref="ItemViewModel.PropertiesDisplay"/> when set; otherwise a count
/// summary ("N properties") if the underlying item has properties; otherwise "None".
/// Mirrors the Quartermaster <c>InventoryPanel</c> fallback rules so the shared control
/// preserves existing behavior across tools.
/// </summary>
public sealed class ItemViewModelPropertiesDisplayConverter : IValueConverter
{
    public static readonly ItemViewModelPropertiesDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ItemViewModel vm)
            return string.Empty;

        if (!string.IsNullOrEmpty(vm.PropertiesDisplay))
            return vm.PropertiesDisplay;

        if (vm.PropertyCount > 0)
            return $"{vm.PropertyCount} properties";

        return "None";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
