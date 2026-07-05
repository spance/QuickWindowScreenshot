namespace QuickWindowScreenshot;

internal sealed record DisplayInfo(
    string DeviceName,
    Rectangle Bounds,
    Rectangle WorkingArea,
    bool Primary);
