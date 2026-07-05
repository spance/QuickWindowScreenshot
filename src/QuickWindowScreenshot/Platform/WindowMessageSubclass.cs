using System.Runtime.InteropServices;

namespace QuickWindowScreenshot.App.Platform;

internal sealed class WindowMessageSubclass : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly UIntPtr _subclassId = new(0x5153);
    private readonly SubclassProc _proc;
    private readonly Func<int, IntPtr, IntPtr, bool> _tryHandleMessage;
    private bool _disposed;

    public WindowMessageSubclass(IntPtr hwnd, Func<int, IntPtr, IntPtr, bool> tryHandleMessage)
    {
        _hwnd = hwnd;
        _tryHandleMessage = tryHandleMessage;
        _proc = WndProc;

        if (!SetWindowSubclass(_hwnd, _proc, _subclassId, UIntPtr.Zero))
        {
            throw new InvalidOperationException($"SetWindowSubclass failed: {Marshal.GetLastWin32Error()}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        RemoveWindowSubclass(_hwnd, _proc, _subclassId);
        _disposed = true;
    }

    private IntPtr WndProc(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr refData)
    {
        if (_tryHandleMessage(message, wParam, lParam))
        {
            return IntPtr.Zero;
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private delegate IntPtr SubclassProc(
        IntPtr hWnd,
        int uMsg,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr uIdSubclass,
        UIntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        UIntPtr uIdSubclass,
        UIntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        UIntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(
        IntPtr hWnd,
        int uMsg,
        IntPtr wParam,
        IntPtr lParam);
}
