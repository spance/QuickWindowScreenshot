namespace QuickWindowScreenshot;

internal interface ICaptureBackend : IDisposable
{
    string Id { get; }

    Bitmap Capture(CaptureBackendRequest request);
}
