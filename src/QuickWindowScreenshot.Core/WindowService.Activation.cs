namespace QuickWindowScreenshot;

internal static partial class WindowService
{
    public static bool IsWindowMinimized(IntPtr hwnd) => NativeMethods.IsIconic(hwnd);

    public static bool IsWindowCloaked(IntPtr hwnd)
    {
        int result = NativeMethods.DwmGetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_CLOAKED,
            out int cloaked,
            sizeof(int));
        return result == 0 && cloaked != 0;
    }

    public static bool IsValidWindow(IntPtr hwnd) => hwnd != IntPtr.Zero && NativeMethods.IsWindow(hwnd);

    public static void RestoreWindow(IntPtr hwnd)
    {
        if (IsWindowMinimized(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        }
    }

    public static bool BringToForeground(IntPtr hwnd)
    {
        RestoreWindow(hwnd);
        return NativeMethods.SetForegroundWindow(hwnd);
    }

    public static bool BringToForegroundAndWait(IntPtr hwnd, int timeoutMilliseconds = 700)
    {
        if (!IsValidWindow(hwnd))
        {
            return false;
        }

        RestoreWindow(hwnd);
        _ = NativeMethods.SetForegroundWindow(hwnd);

        long deadline = Environment.TickCount64 + timeoutMilliseconds;
        do
        {
            if (NativeMethods.GetForegroundWindow() == hwnd)
            {
                return true;
            }
            Thread.Sleep(35);
        }
        while (Environment.TickCount64 < deadline);

        return NativeMethods.GetForegroundWindow() == hwnd;
    }

    public static IntPtr GetForegroundWindow() => NativeMethods.GetForegroundWindow();
}
