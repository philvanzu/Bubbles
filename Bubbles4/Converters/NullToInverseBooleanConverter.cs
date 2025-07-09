namespace Bubbles4.Converters;

using System;
using System.Globalization;
using Avalonia.Data.Converters;

public class NullToInverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}