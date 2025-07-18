using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Bubbles4.Converters;

public class ChevronRotationConverter : IValueConverter
{
    public Geometry RightChevron { get; set; } = Geometry.Parse("M 2 4 L 8 10 L 2 16");
    public Geometry DownChevron { get; set; } = Geometry.Parse("M 4 2 L 10 8 L 16 2");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isChecked && isChecked ? DownChevron : RightChevron;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}