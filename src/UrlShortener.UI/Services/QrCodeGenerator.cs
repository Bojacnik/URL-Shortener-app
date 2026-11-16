using QRCoder;

namespace UrlShortener.UI.Services;

public static class QrCodeGenerator
{
    /// <summary>PNG bytes of a QR code for <paramref name="text"/>; pure managed code, no System.Drawing.</summary>
    public static byte[] CreatePng(string text, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }
}
