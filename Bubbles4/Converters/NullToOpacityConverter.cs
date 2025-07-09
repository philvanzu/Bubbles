using System;
using System.Collections.Generic;
using Avalonia.Data.Converters;

namespace Bubbles4.Converters;

public class NullToOpacityConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return values[0] == null ? 1.0 : 0.0;
    }
}