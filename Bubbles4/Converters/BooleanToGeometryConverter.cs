using Avalonia.Media;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Bubbles4.Converters;
public class BooleanToGeometryConverter : IValueConverter
{
    public Geometry UpGeometry { get; set; } = Geometry.Parse("M 4 10 L 8 6 L 12 10");
    public Geometry DownGeometry { get; set; } = Geometry.Parse("M 4 6 L 8 10 L 12 6");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? UpGeometry : DownGeometry;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}