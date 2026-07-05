namespace QuickWindowScreenshot;

internal static class HotkeyParser
{
    private static readonly Dictionary<string, uint> KeyCodes = BuildKeyCodes();

    public static HotkeyDefinition Parse(string text)
    {
        string[] parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            throw new ArgumentException("请输入快捷键，例如 Ctrl+Alt+F12");
        }

        uint modifiers = NativeMethods.MOD_NOREPEAT;
        uint? key = null;
        List<string> normalized = [];

        foreach (string rawPart in parts)
        {
            string part = NormalizeToken(rawPart);
            if (part is "CTRL" or "CONTROL")
            {
                modifiers |= NativeMethods.MOD_CONTROL;
                AddModifierOnce(normalized, "Ctrl");
            }
            else if (part == "ALT")
            {
                modifiers |= NativeMethods.MOD_ALT;
                AddModifierOnce(normalized, "Alt");
            }
            else if (part == "SHIFT")
            {
                modifiers |= NativeMethods.MOD_SHIFT;
                AddModifierOnce(normalized, "Shift");
            }
            else if (part is "WIN" or "WINDOWS" or "META" or "SUPER" or "CMD" or "COMMAND")
            {
                modifiers |= NativeMethods.MOD_WIN;
                AddModifierOnce(normalized, "Win");
            }
            else if (KeyCodes.TryGetValue(part, out uint vk))
            {
                if (key.HasValue)
                {
                    throw new ArgumentException("快捷键只能包含一个主按键");
                }
                key = vk;
                normalized.Add(FormatKeyText(part));
            }
            else
            {
                throw new ArgumentException($"不支持的按键: {rawPart}");
            }
        }

        if (!key.HasValue)
        {
            throw new ArgumentException("快捷键缺少主按键");
        }

        return new HotkeyDefinition(modifiers, key.Value, string.Join("+", normalized));
    }

    private static Dictionary<string, uint> BuildKeyCodes()
    {
        Dictionary<string, uint> codes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["BACKSPACE"] = 0x08,
            ["BACK"] = 0x08,
            ["TAB"] = 0x09,
            ["ENTER"] = 0x0D,
            ["RETURN"] = 0x0D,
            ["ESC"] = 0x1B,
            ["ESCAPE"] = 0x1B,
            ["SPACE"] = 0x20,
            ["SPACEBAR"] = 0x20,
            ["PGUP"] = 0x21,
            ["PAGEUP"] = 0x21,
            ["PAGEDOWN"] = 0x22,
            ["PGDN"] = 0x22,
            ["END"] = 0x23,
            ["HOME"] = 0x24,
            ["LEFT"] = 0x25,
            ["UP"] = 0x26,
            ["RIGHT"] = 0x27,
            ["DOWN"] = 0x28,
            ["PRINT"] = 0x2C,
            ["PRINTSCREEN"] = 0x2C,
            ["PRTSC"] = 0x2C,
            ["PRTSCR"] = 0x2C,
            ["INS"] = 0x2D,
            ["INSERT"] = 0x2D,
            ["DEL"] = 0x2E,
            ["DELETE"] = 0x2E,
        };

        for (int i = 1; i <= 24; i++)
        {
            codes[$"F{i}"] = (uint)(0x70 + i - 1);
        }
        for (char c = 'A'; c <= 'Z'; c++)
        {
            codes[c.ToString()] = c;
        }
        for (char c = '0'; c <= '9'; c++)
        {
            codes[c.ToString()] = c;
            codes[$"D{c}"] = c;
        }
        return codes;
    }

    private static string NormalizeToken(string token) => token.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    private static void AddModifierOnce(List<string> parts, string modifier)
    {
        if (!parts.Contains(modifier))
        {
            parts.Add(modifier);
        }
    }

    private static string FormatKeyText(string token) => token switch
    {
        "ESCAPE" => "Esc",
        "DELETE" => "Del",
        "INSERT" => "Ins",
        "PAGEUP" or "PGUP" => "PageUp",
        "PAGEDOWN" or "PGDN" => "PageDown",
        "PRINTSCREEN" or "PRINT" or "PRTSC" or "PRTSCR" => "PrintScreen",
        "SPACE" or "SPACEBAR" => "Space",
        _ when token.StartsWith('D') && token.Length == 2 && char.IsDigit(token[1]) => token[1].ToString(),
        _ => token.Length == 1 ? token : token[0] + token[1..].ToLowerInvariant(),
    };
}
