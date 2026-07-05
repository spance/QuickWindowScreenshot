namespace QuickWindowScreenshot;

internal sealed record ChildWindowInfo(
    IntPtr Hwnd,
    string ClassName,
    Rectangle Rect,
    bool Visible);
