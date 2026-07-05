using System.Runtime.InteropServices;

namespace QuickWindowScreenshot;

internal static partial class WindowService
{
    public static Rectangle GetContentRectScreen(IntPtr hwnd) =>
        GetWindowsTerminalContentRect(hwnd) ?? GetClientRectScreen(hwnd);

    public static Rectangle? GetWindowsTerminalContentRect(IntPtr hwnd)
    {
        if (!string.Equals(GetClassName(hwnd), "CASCADIA_HOSTING_WINDOW_CLASS", StringComparison.Ordinal))
        {
            return null;
        }

        List<ChildWindowInfo> children = EnumerateChildWindows(hwnd);
        ChildWindowInfo? xaml = children
            .Where(child =>
                child.Visible &&
                child.ClassName == "Windows.UI.Composition.DesktopWindowContentBridge" &&
                child.Rect.Width > 20 &&
                child.Rect.Height > 20)
            .OrderByDescending(child => child.Rect.Width * child.Rect.Height)
            .FirstOrDefault();

        if (xaml is null)
        {
            return null;
        }

        Rectangle rect = xaml.Rect;
        int top = rect.Top;
        foreach (ChildWindowInfo dragBar in children.Where(child =>
            child.Visible &&
            child.ClassName == "DRAG_BAR_WINDOW_CLASS" &&
            RectsOverlapHorizontally(child.Rect, rect)))
        {
            top = Math.Max(top, dragBar.Rect.Bottom);
        }

        if (rect.Right <= rect.Left || rect.Bottom <= top)
        {
            return null;
        }

        return Rectangle.FromLTRB(rect.Left, top, rect.Right, rect.Bottom);
    }

    public static Rectangle GetClientRectScreen(IntPtr hwnd)
    {
        using NativeMethods.ThreadDpiScope _ = NativeMethods.EnterPerMonitorDpiScope();
        if (!NativeMethods.GetClientRect(hwnd, out NativeMethods.RECT rect))
        {
            throw NativeMethods.LastWin32Exception();
        }

        NativeMethods.POINT[] points =
        [
            new(rect.Left, rect.Top),
            new(rect.Right, rect.Bottom),
        ];
        NativeMethods.MapWindowPoints(hwnd, IntPtr.Zero, points, 2);
        return Rectangle.FromLTRB(points[0].X, points[0].Y, points[1].X, points[1].Y);
    }

    public static Rectangle? GetWindowInfoClientRectScreen(IntPtr hwnd)
    {
        NativeMethods.WINDOWINFO? info = GetWindowInfo(hwnd);
        if (info is null)
        {
            return null;
        }
        if (info.Value.rcClient.Width <= 0 || info.Value.rcClient.Height <= 0)
        {
            return null;
        }
        return info.Value.rcClient.ToRectangle();
    }

    public static NativeMethods.WINDOWINFO? GetWindowInfo(IntPtr hwnd)
    {
        using NativeMethods.ThreadDpiScope _ = NativeMethods.EnterPerMonitorDpiScope();
        NativeMethods.WINDOWINFO info = new()
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WINDOWINFO>(),
        };
        if (!NativeMethods.GetWindowInfo(hwnd, ref info))
        {
            return null;
        }
        return info;
    }

    public static Rectangle GetWindowRectScreen(IntPtr hwnd)
    {
        using NativeMethods.ThreadDpiScope _ = NativeMethods.EnterPerMonitorDpiScope();
        if (!NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT rect))
        {
            throw NativeMethods.LastWin32Exception();
        }
        return rect.ToRectangle();
    }

    public static Rectangle GetVisibleFrameRectScreen(IntPtr hwnd)
    {
        using NativeMethods.ThreadDpiScope _ = NativeMethods.EnterPerMonitorDpiScope();
        int result = NativeMethods.DwmGetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out NativeMethods.RECT rect,
            (uint)Marshal.SizeOf<NativeMethods.RECT>());
        return result == 0 ? rect.ToRectangle() : GetWindowRectScreen(hwnd);
    }

    private static bool RectsOverlapHorizontally(Rectangle first, Rectangle second) =>
        Math.Min(first.Right, second.Right) > Math.Max(first.Left, second.Left);
}
