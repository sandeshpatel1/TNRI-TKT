using QRCoder;

namespace TanaririTickets.Services;

public static class QrService
{
    public static string ToDataUri(string text)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(12);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
