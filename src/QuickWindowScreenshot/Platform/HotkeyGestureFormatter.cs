using System.Runtime.InteropServices;
using Windows.System;

namespace QuickWindowScreenshot.App.Platform;

internal static class HotkeyGestureFormatter
{
    public static string Format(VirtualKey key)
    {
        List<string> parts = [];
        if (IsKeyDown(0x11) || key == VirtualKey.Control)
        {
            parts.Add("Ctrl");
        }
        if (IsKeyDown(0x12) || key == VirtualKey.Menu)
        {
            parts.Add("Alt");
        }
        if (IsKeyDown(0x10) || key == VirtualKey.Shift)
        {
            parts.Add("Shift");
        }
        if (IsKeyDown(0x5B) || IsKeyDown(0x5C) || key is VirtualKey.LeftWindows or VirtualKey.RightWindows)
        {
            parts.Add("Win");
        }

        if (IsModifierKey(key))
        {
            return string.Join("+", parts);
        }

        parts.Add(FormatPrimaryKey(key));
        return string.Join("+", parts);
    }

    private static bool IsKeyDown(int virtualKey) => (GetKeyState(virtualKey) & 0x8000) != 0;

    private static bool IsModifierKey(VirtualKey key) =>
        key is VirtualKey.Control
            or VirtualKey.Shift
            or VirtualKey.Menu
            or VirtualKey.LeftControl
            or VirtualKey.RightControl
            or VirtualKey.LeftShift
            or VirtualKey.RightShift
            or VirtualKey.LeftMenu
            or VirtualKey.RightMenu
            or VirtualKey.LeftWindows
            or VirtualKey.RightWindows;

    private static string FormatPrimaryKey(VirtualKey key)
    {
        int value = (int)key;
        if (value is >= 0x41 and <= 0x5A)
        {
            return ((char)value).ToString();
        }
        if (value is >= 0x30 and <= 0x39)
        {
            return ((char)value).ToString();
        }
        if (value is >= 0x60 and <= 0x69)
        {
            return (value - 0x60).ToString();
        }
        if (value is >= 0x70 and <= 0x87)
        {
            return $"F{value - 0x6F}";
        }

        return key switch
        {
            VirtualKey.Escape => "Esc",
            VirtualKey.Delete => "Del",
            VirtualKey.Insert => "Ins",
            VirtualKey.PageUp => "PageUp",
            VirtualKey.PageDown => "PageDown",
            VirtualKey.Space => "Space",
            (VirtualKey)0x2C => "PrintScreen",
            _ => key.ToString(),
        };
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);
}
