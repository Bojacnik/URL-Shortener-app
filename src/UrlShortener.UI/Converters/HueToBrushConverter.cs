using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace UrlShortener.UI.Converters;

/// <summary>Turns a hue (0–359) into a pleasant, saturated badge colour that works with white text.</summary>
public sealed class HueToBrushConverter : IValueConverter
{
    public static readonly HueToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int hue ? new SolidColorBrush(FromHsl(hue, 0.58, 0.52)) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    internal static Color FromHsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = l - c / 2;
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
