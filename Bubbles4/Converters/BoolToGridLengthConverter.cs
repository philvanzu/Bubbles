using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Bubbles4.Converters;

public class BoolToGridLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool show = value is bool b && b;
        string? param = parameter as string;

        // Customize behavior based on parameter
        return param switch
        {
            "NavPane" => show ? new GridLength(300) : new GridLength(0),
            "Splitter" => show ? new GridLength(1, GridUnitType.Auto) : new GridLength(0), // fixed width or 0
            _ => new GridLength(0)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

