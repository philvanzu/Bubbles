using Avalonia;
using System;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Bubbles4.Converters;
public class InverseBooleanConverter : IValueConverter
{
    public static readonly InverseBooleanConverter Instance = new InverseBooleanConverter();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;

        return AvaloniaProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;

        return AvaloniaProperty.UnsetValue;
    }
}