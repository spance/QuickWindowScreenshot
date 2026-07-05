namespace QuickWindowScreenshot;

internal sealed record CaptureBackendRequest(
    IntPtr Hwnd,
    Rectangle ContentRect,
    DisplayInfo Display);
