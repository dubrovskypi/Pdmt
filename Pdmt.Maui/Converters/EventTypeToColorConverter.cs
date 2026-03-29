using System.Globalization;

namespace Pdmt.Maui.Converters;

public class EventTypeToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int type && type == 1 ? Color.FromArgb("#2E7D32") : Color.FromArgb("#C62828");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
