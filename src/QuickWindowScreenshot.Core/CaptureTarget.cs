namespace QuickWindowScreenshot;

internal sealed record CaptureTarget(
    IntPtr Hwnd,
    Rectangle ContentRect,
    DisplayInfo Display);
