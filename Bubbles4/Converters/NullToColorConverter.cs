namespace Bubbles4.Converters;

using System;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

public class NullToColorConverter : IValueConverter
{
    public IBrush NullBrush { get; set; } = Brushes.Gray;
    public IBrush NotNullBrush { get; set; } = Brushes.White;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null ? NullBrush : NotNullBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}