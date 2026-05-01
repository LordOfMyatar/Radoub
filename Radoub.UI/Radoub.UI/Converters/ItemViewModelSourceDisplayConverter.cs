using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Radoub.UI.ViewModels;

namespace Radoub.UI.Converters;

/// <summary>
/// Converts an <see cref="ItemViewModel"/> into a display string for its source.
/// Returns <see cref="ItemViewModel.SourceLocation"/> when set, otherwise the
/// <see cref="ItemViewModel.Source"/> enum name (e.g., "Bif", "Override", "Hak", "Module").
/// </summary>
public sealed class ItemViewModelSourceDisplayConverter : IValueConverter
{
    public static readonly ItemViewModelSourceDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ItemViewModel vm)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(vm.SourceLocation))
            return vm.SourceLocation;

        return vm.Source.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
