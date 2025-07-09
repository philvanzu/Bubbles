using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Bubbles4.Converters
{
    public class NullToBooleanConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool result = value != null;
            return Invert ? !result : result;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}