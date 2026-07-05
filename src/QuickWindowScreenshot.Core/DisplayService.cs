using System.ComponentModel;
using System.Runtime.InteropServices;

namespace QuickWindowScreenshot;

internal static class DisplayService
{
    public static IReadOnlyList<DisplayInfo> EnumerateDisplays()
    {
        List<DisplayInfo> displays = [];

        bool Callback(IntPtr monitor, IntPtr _, ref NativeMethods.RECT __, IntPtr ___)
        {
            displays.Add(GetMonitorInfo(monitor));
            return true;
        }

        if (!NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero))
        {
            throw NativeMethods.LastWin32Exception();
        }

        return displays;
    }

    public static DisplayInfo FromCursorPosition()
    {
        if (!NativeMethods.GetCursorPos(out NativeMethods.POINT point))
        {
            throw NativeMethods.LastWin32Exception();
        }

        return FromPoint(new Point(point.X, point.Y));
    }

    public static DisplayInfo FromPoint(Point point)
    {
        IntPtr monitor = NativeMethods.MonitorFromPoint(
            new NativeMethods.POINT(point.X, point.Y),
            NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
        {
            throw new Win32Exception("No display monitor contains the requested point.");
        }

        return GetMonitorInfo(monitor);
    }

    public static DisplayInfo? GetContainingDisplay(Rectangle rect) =>
        EnumerateDisplays().FirstOrDefault(display => display.Bounds.Contains(rect));

    private static DisplayInfo GetMonitorInfo(IntPtr monitor)
    {
        NativeMethods.MONITORINFOEX info = new()
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
        };

        if (!NativeMethods.GetMonitorInfoW(monitor, ref info))
        {
            throw NativeMethods.LastWin32Exception();
        }

        return new DisplayInfo(
            info.szDevice,
            info.rcMonitor.ToRectangle(),
            info.rcWork.ToRectangle(),
            (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0);
    }
}
