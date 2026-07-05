using System.Text;

namespace QuickWindowScreenshot;

internal static partial class WindowService
{
    public static string DefaultOutputDir()
    {
        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrWhiteSpace(pictures))
        {
            pictures = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures");
        }
        return Path.Combine(pictures, "Quick Window Screenshot");
    }

    public static string SettingsPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Quick Window Screenshot", "settings.json");
    }

    public static string LegacySettingsPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "QuickScreenshot", "settings.json");
    }

    public static string GetWindowText(IntPtr hwnd)
    {
        int length = NativeMethods.GetWindowTextLengthW(hwnd);
        if (length <= 0)
        {
            return "";
        }

        StringBuilder buffer = new(length + 1);
        NativeMethods.GetWindowTextW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString().Trim();
    }

    public static string GetClassName(IntPtr hwnd)
    {
        StringBuilder buffer = new(256);
        NativeMethods.GetClassNameW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    public static uint GetWindowPid(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid;
    }

    public static string GetProcessName(uint pid)
    {
        if (pid == 0)
        {
            return "";
        }

        IntPtr handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero)
        {
            return "";
        }

        try
        {
            int size = 32768;
            StringBuilder buffer = new(size);
            if (NativeMethods.QueryFullProcessImageNameW(handle, 0, buffer, ref size))
            {
                return Path.GetFileName(buffer.ToString());
            }
            return "";
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }
}
