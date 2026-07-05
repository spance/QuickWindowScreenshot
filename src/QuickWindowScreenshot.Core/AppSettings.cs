using System.Text.Json;

namespace QuickWindowScreenshot;

internal sealed class AppSettings
{
    public const decimal DefaultIntervalSeconds = 3;
    public const string DefaultPrefix = "qws";
    public const string DefaultHotkey = "Ctrl+Alt+F12";
    private const decimal MaximumIntervalSeconds = 86400;
    private const string LegacyDefaultPrefix = "shot";

    public string OutputDir { get; set; } = WindowService.DefaultOutputDir();
    public string Hotkey { get; set; } = DefaultHotkey;
    public decimal IntervalSeconds { get; set; } = DefaultIntervalSeconds;
    public string Prefix { get; set; } = DefaultPrefix;
    public string Backend { get; set; } = CaptureBackendIds.Wgc;
    public bool BringToFront { get; set; }
    public string TargetMode { get; set; } = CaptureTargetModes.WindowContent;

    public static AppSettings Load()
    {
        string path = File.Exists(WindowService.SettingsPath())
            ? WindowService.SettingsPath()
            : WindowService.LegacySettingsPath();
        if (!File.Exists(path))
        {
            return Normalize(new AppSettings());
        }

        try
        {
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
            return Normalize(settings ?? new AppSettings());
        }
        catch
        {
            return Normalize(new AppSettings());
        }
    }

    internal static AppSettings Normalize(AppSettings settings)
    {
        settings.OutputDir = string.IsNullOrWhiteSpace(settings.OutputDir)
            ? WindowService.DefaultOutputDir()
            : settings.OutputDir.Trim();
        settings.Hotkey = string.IsNullOrWhiteSpace(settings.Hotkey)
            ? DefaultHotkey
            : settings.Hotkey.Trim();
        settings.IntervalSeconds = Math.Clamp(settings.IntervalSeconds, 1, MaximumIntervalSeconds);
        settings.Backend = string.IsNullOrWhiteSpace(settings.Backend)
            ? CaptureBackendIds.Wgc
            : CaptureBackendIds.Normalize(settings.Backend);
        settings.TargetMode = CaptureTargetModes.Normalize(settings.TargetMode);

        string prefix = settings.Prefix?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(prefix)
            || string.Equals(prefix, LegacyDefaultPrefix, StringComparison.OrdinalIgnoreCase))
        {
            prefix = DefaultPrefix;
        }

        settings.Prefix = prefix;
        return settings;
    }

    public void Save()
    {
        string path = WindowService.SettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(this, options));
    }
}
