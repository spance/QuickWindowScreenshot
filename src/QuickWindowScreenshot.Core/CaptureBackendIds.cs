namespace QuickWindowScreenshot;

internal static class CaptureBackendIds
{
    public const string Wgc = "wgc";
    public const string Dxgi = "dxgi";
    public const string Gdi = "gdi";
    public const string WinRtAlias = "winrt";

    public static string Normalize(string backend)
    {
        if (string.Equals(backend, Wgc, StringComparison.OrdinalIgnoreCase)
            || string.Equals(backend, WinRtAlias, StringComparison.OrdinalIgnoreCase))
        {
            return Wgc;
        }

        return string.Equals(backend, Gdi, StringComparison.OrdinalIgnoreCase) ? Gdi : Dxgi;
    }
}
