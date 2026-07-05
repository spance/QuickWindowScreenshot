using System.Drawing.Imaging;

namespace QuickWindowScreenshot;

internal sealed class GdiCaptureBackend : ICaptureBackend
{
    public string Id => CaptureBackendIds.Gdi;

    public Bitmap Capture(CaptureBackendRequest request)
    {
        Rectangle rect = request.ContentRect;
        Bitmap bitmap = new(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    public void Dispose()
    {
    }
}
