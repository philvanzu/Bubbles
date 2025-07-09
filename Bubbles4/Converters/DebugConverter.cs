using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Bubbles4.Converters;

public class DebugConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        Debug.WriteLine("CONVERT: " + value?.GetType().Name);
        return value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}