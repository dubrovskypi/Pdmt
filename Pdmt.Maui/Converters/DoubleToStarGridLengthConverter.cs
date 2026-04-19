using System.Globalization;

namespace Pdmt.Maui.Converters;

public class DoubleToStarGridLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double d = value is double v ? Math.Clamp(v, 0, 1) : 0;
        if (parameter is string p && p == "inverse") d = 1 - d;
        return new GridLength(Math.Max(d, 0.001), GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
