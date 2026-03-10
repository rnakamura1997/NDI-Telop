using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace NdiTelop.Converters;

public class StringToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string colorString && targetType == typeof(Color))
        {
            try
            {
                return Color.Parse(colorString);
            }
            catch (FormatException)
            {
                // Invalid color string, return a default or transparent color
                return Colors.Transparent;
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color color && targetType == typeof(string))
        {
            return color.ToString();
        }
        return null;
    }
}
