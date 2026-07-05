namespace QuickWindowScreenshot;

internal interface ICaptureTargetResolver
{
    CaptureTarget Resolve(CaptureRequest request);
}
