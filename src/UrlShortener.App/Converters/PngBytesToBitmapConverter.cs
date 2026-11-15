using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace UrlShortener.App.Converters;

public sealed class PngBytesToBitmapConverter : IValueConverter
{
    public static readonly PngBytesToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes) return null;
        using var stream = new MemoryStream(bytes);
        return new Bitmap(stream);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
