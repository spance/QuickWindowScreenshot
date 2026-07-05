namespace QuickWindowScreenshot;

internal static class CaptureTargetModes
{
    public const string WindowContent = "windowContent";
    public const string FullScreenTarget = "fullScreenTarget";

    public static string Normalize(string mode) =>
        string.Equals(mode, FullScreenTarget, StringComparison.OrdinalIgnoreCase)
            ? FullScreenTarget
            : WindowContent;
}
