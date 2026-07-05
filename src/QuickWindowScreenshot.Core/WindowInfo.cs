namespace QuickWindowScreenshot;

internal sealed record WindowInfo(
    IntPtr Hwnd,
    string Title,
    uint Pid,
    string ProcessName,
    Rectangle ClientRect,
    bool Minimized,
    string ClassName = "")
{
    public string DisplayName
    {
        get
        {
            string process = FriendlyProcessName(ProcessName);
            string title = FriendlyTitle(Title);
            string state = Minimized ? " · minimized" : "";
            return $"{TrimMiddle(process, 18)} · {TrimMiddle(title, 46)} · {ClientRect.Width}x{ClientRect.Height}{state}";
        }
    }

    private static string FriendlyProcessName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        string name = Path.GetFileNameWithoutExtension(value.Trim());
        return string.IsNullOrWhiteSpace(name) ? value : name;
    }

    private static string FriendlyTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "[untitled]";
        }

        string title = value.Trim().TrimStart('*').Trim();
        int dashIndex = title.LastIndexOf(" - ", StringComparison.Ordinal);
        if (dashIndex > 0)
        {
            title = title[..dashIndex].Trim();
        }

        string fileName = Path.GetFileName(title);
        return string.IsNullOrWhiteSpace(fileName) ? title : fileName;
    }

    private static string TrimMiddle(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        int head = Math.Max(1, (maxLength - 1) / 2);
        int tail = Math.Max(1, maxLength - head - 1);
        return value[..head] + "…" + value[^tail..];
    }
}
