using System.ComponentModel;

namespace QuickWindowScreenshot;

internal static partial class WindowService
{
    private const int MinimumListedWindowSize = 64;

    private static readonly HashSet<string> ExcludedWindowClasses = new(StringComparer.Ordinal)
    {
        "ThumbnailDeviceHelperWnd",
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
    };

    public static IReadOnlyList<WindowInfo> EnumerateWindows()
    {
        List<WindowInfo> windows = [];
        Dictionary<uint, string> processCache = [];

        bool Callback(IntPtr hwnd, IntPtr _)
        {
            if (!NativeMethods.IsWindowVisible(hwnd))
            {
                return true;
            }

            string title = GetWindowText(hwnd);
            string className = GetClassName(hwnd);
            if (!IsListedWindowCandidate(hwnd, title, className))
            {
                return true;
            }

            Rectangle clientRect;
            try
            {
                clientRect = GetClientRectScreen(hwnd);
            }
            catch (Win32Exception)
            {
                return true;
            }

            if (clientRect.Width < MinimumListedWindowSize || clientRect.Height < MinimumListedWindowSize)
            {
                return true;
            }

            uint pid = GetWindowPid(hwnd);
            if (pid == Environment.ProcessId)
            {
                return true;
            }

            if (!processCache.TryGetValue(pid, out string? processName))
            {
                processName = GetProcessName(pid);
                processCache[pid] = processName;
            }

            windows.Add(new WindowInfo(hwnd, title, pid, processName, clientRect, IsWindowMinimized(hwnd), className));
            return true;
        }

        if (!NativeMethods.EnumWindows(Callback, IntPtr.Zero))
        {
            throw NativeMethods.LastWin32Exception();
        }

        return windows.OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static WindowInfo? FindWindow(IntPtr hwnd) =>
        IsValidWindow(hwnd) ? CreateWindowInfo(hwnd) : null;

    public static WindowInfo? CreateWindowInfo(IntPtr hwnd)
    {
        if (!IsValidWindow(hwnd))
        {
            return null;
        }

        Rectangle clientRect;
        try
        {
            clientRect = GetClientRectScreen(hwnd);
        }
        catch (Win32Exception)
        {
            return null;
        }

        if (clientRect.Width <= 0 || clientRect.Height <= 0)
        {
            return null;
        }

        uint pid = GetWindowPid(hwnd);
        return new WindowInfo(
            hwnd,
            GetWindowText(hwnd),
            pid,
            GetProcessName(pid),
            clientRect,
            IsWindowMinimized(hwnd),
            GetClassName(hwnd));
    }

    public static List<ChildWindowInfo> EnumerateChildWindows(IntPtr hwnd)
    {
        List<ChildWindowInfo> children = [];

        bool Callback(IntPtr childHwnd, IntPtr _)
        {
            Rectangle rect;
            try
            {
                rect = GetWindowRectScreen(childHwnd);
            }
            catch (Win32Exception)
            {
                return true;
            }

            children.Add(new ChildWindowInfo(
                childHwnd,
                GetClassName(childHwnd),
                rect,
                NativeMethods.IsWindowVisible(childHwnd)));
            return true;
        }

        NativeMethods.EnumChildWindows(hwnd, Callback, IntPtr.Zero);
        return children;
    }

    private static bool IsListedWindowCandidate(IntPtr hwnd, string title, string className)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        if (IsWindowMinimized(hwnd) || IsWindowCloaked(hwnd))
        {
            return false;
        }

        NativeMethods.WINDOWINFO? info = GetWindowInfo(hwnd);
        if (info is not null && (info.Value.dwExStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        return !ExcludedWindowClasses.Contains(className);
    }
}
