using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Bubbles4.Converters;


public class ChevronOffsetConverter : IValueConverter
{
    // TranslateTransform: shift right when collapsed, down when expanded
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
        {
            return isExpanded
                ? new TranslateTransform(0, 4)   // Down
                : new TranslateTransform(4, 0);  // Right
        }

        return new TranslateTransform(); // Default (no shift)
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
