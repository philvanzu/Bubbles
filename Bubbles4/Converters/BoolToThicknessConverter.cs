using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Bubbles4.Converters;

public class BoolToThicknessConverter : IValueConverter
{
    public Thickness TrueThickness { get; set; } = new Thickness(3);
    public Thickness FalseThickness { get; set; } = new Thickness(1);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return TrueThickness;
        return FalseThickness;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}