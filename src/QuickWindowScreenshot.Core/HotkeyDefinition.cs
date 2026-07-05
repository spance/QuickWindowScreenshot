namespace QuickWindowScreenshot;

internal sealed record HotkeyDefinition(uint Modifiers, uint Key, string Text)
{
    public static HotkeyDefinition Parse(string text) => HotkeyParser.Parse(text);
}
