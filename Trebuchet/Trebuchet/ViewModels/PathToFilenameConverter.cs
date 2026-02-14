using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace RadoubLauncher.ViewModels;

/// <summary>
/// Converts a full file path to just the filename without extension.
/// Used in the sidebar recent modules list for compact display.
/// </summary>
public class PathToFilenameConverter : IValueConverter
{
    public static readonly PathToFilenameConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
            return Path.GetFileNameWithoutExtension(path);
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
