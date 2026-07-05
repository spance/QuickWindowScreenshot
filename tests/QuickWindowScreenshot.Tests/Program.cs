using System.Drawing;

namespace QuickWindowScreenshot.Tests;

internal static class Program
{
    private static int _failed;

    private static void Main()
    {
        Run("Hotkey parsing normalizes common forms", HotkeyParsingNormalizesCommonForms);
        Run("Hotkey parsing rejects multiple primary keys", HotkeyParsingRejectsMultiplePrimaryKeys);
        Run("Hotkey parsing deduplicates modifiers and aliases", HotkeyParsingDeduplicatesModifiersAndAliases);
        Run("Untitled windows get a stable display name", UntitledWindowsGetStableDisplayName);
        Run("Backend aliases normalize consistently", BackendAliasesNormalizeConsistently);
        Run("Default settings are release-ready", DefaultSettingsAreReleaseReady);
        Run("Legacy default prefix migrates to qws", LegacyDefaultPrefixMigratesToQws);
        Run("Invalid settings normalize to safe defaults", InvalidSettingsNormalizeToSafeDefaults);
        Run("Capture file names sanitize prefix and use timestamp", CaptureFileNamesSanitizePrefixAndUseTimestamp);
        Run("Capture service uses normalized backend and writes PNG", CaptureServiceUsesNormalizedBackendAndWritesPng);
        Run("Fullscreen target falls back from WGC to DXGI", FullscreenTargetFallsBackFromWgcToDxgi);

        if (_failed > 0)
        {
            Console.Error.WriteLine($"{_failed} test(s) failed.");
            Environment.Exit(1);
        }

        Console.WriteLine("All tests passed.");
    }

    private static void HotkeyParsingNormalizesCommonForms()
    {
        HotkeyDefinition definition = HotkeyDefinition.Parse(" control + shift + s ");
        AssertEqual("Ctrl+Shift+S", definition.Text);
        AssertTrue((definition.Modifiers & NativeMethods.MOD_CONTROL) != 0);
        AssertTrue((definition.Modifiers & NativeMethods.MOD_SHIFT) != 0);
        AssertEqual((uint)'S', definition.Key);

        HotkeyDefinition print = HotkeyDefinition.Parse("Alt+PrintScreen");
        AssertEqual("Alt+PrintScreen", print.Text);
        AssertEqual(0x2Cu, print.Key);

        HotkeyDefinition defaultHotkey = HotkeyDefinition.Parse("ctrl + alt + f12");
        AssertEqual("Ctrl+Alt+F12", defaultHotkey.Text);
        AssertEqual(0x7Bu, defaultHotkey.Key);
    }

    private static void HotkeyParsingRejectsMultiplePrimaryKeys()
    {
        AssertThrows<ArgumentException>(() => HotkeyDefinition.Parse("Ctrl+A+B"));
        AssertThrows<ArgumentException>(() => HotkeyDefinition.Parse("Ctrl+Shift"));
    }

    private static void HotkeyParsingDeduplicatesModifiersAndAliases()
    {
        HotkeyDefinition definition = HotkeyDefinition.Parse("Control + Ctrl + Windows + d5");
        AssertEqual("Ctrl+Win+5", definition.Text);
        AssertTrue((definition.Modifiers & NativeMethods.MOD_CONTROL) != 0);
        AssertTrue((definition.Modifiers & NativeMethods.MOD_WIN) != 0);
        AssertEqual((uint)'5', definition.Key);

        HotkeyDefinition pageDown = HotkeyDefinition.Parse("cmd+pgdn");
        AssertEqual("Win+PageDown", pageDown.Text);
    }

    private static void UntitledWindowsGetStableDisplayName()
    {
        WindowInfo window = new(IntPtr.Zero, "", 1234, "game.exe", new Rectangle(10, 20, 800, 600), false);
        string display = window.DisplayName;
        AssertContains("[untitled]", display);
        AssertContains("game", display);
        AssertContains("800x600", display);
        AssertTrue(!display.Contains("hwnd", StringComparison.OrdinalIgnoreCase));

        WindowInfo pathTitle = new(
            IntPtr.Zero,
            @"*C:\Users\spance\Documents\SpNotes\20 Projects\openwrt\GL-iNet BE3600.md - Notepad++",
            1234,
            "notepad++.exe",
            new Rectangle(10, 20, 1024, 768),
            false);
        AssertContains("notepad++", pathTitle.DisplayName);
        AssertContains("GL-iNet BE3600.md", pathTitle.DisplayName);
        AssertTrue(!pathTitle.DisplayName.Contains(@"C:\Users", StringComparison.OrdinalIgnoreCase));
    }

    private static void BackendAliasesNormalizeConsistently()
    {
        AssertEqual("wgc", CaptureService.NormalizeBackend("wgc"));
        AssertEqual("wgc", CaptureService.NormalizeBackend("winrt"));
        AssertEqual("gdi", CaptureService.NormalizeBackend("GDI"));
        AssertEqual("dxgi", CaptureService.NormalizeBackend("unknown"));
    }

    private static void DefaultSettingsAreReleaseReady()
    {
        AppSettings settings = new();
        AssertEqual("wgc", settings.Backend);
        AssertEqual("qws", settings.Prefix);
        AssertEqual("Ctrl+Alt+F12", settings.Hotkey);
        AssertEqual(AppSettings.DefaultIntervalSeconds, settings.IntervalSeconds);
    }

    private static void LegacyDefaultPrefixMigratesToQws()
    {
        AppSettings settings = AppSettings.Normalize(new AppSettings { Prefix = "shot" });
        AssertEqual(AppSettings.DefaultPrefix, settings.Prefix);

        AppSettings customSettings = AppSettings.Normalize(new AppSettings { Prefix = " project " });
        AssertEqual("project", customSettings.Prefix);
    }

    private static void InvalidSettingsNormalizeToSafeDefaults()
    {
        AppSettings settings = AppSettings.Normalize(new AppSettings
        {
            OutputDir = " ",
            Hotkey = "",
            IntervalSeconds = -5,
            Prefix = null!,
            Backend = "",
            TargetMode = "unexpected",
        });

        AssertEqual(WindowService.DefaultOutputDir(), settings.OutputDir);
        AssertEqual(AppSettings.DefaultHotkey, settings.Hotkey);
        AssertEqual(1m, settings.IntervalSeconds);
        AssertEqual(AppSettings.DefaultPrefix, settings.Prefix);
        AssertEqual(CaptureBackendIds.Wgc, settings.Backend);
        AssertEqual(CaptureTargetModes.WindowContent, settings.TargetMode);
    }

    private static void CaptureFileNamesSanitizePrefixAndUseTimestamp()
    {
        TimestampedCaptureFileNamer namer = new(() => new DateTime(2026, 7, 3, 12, 34, 56, 789));
        AssertEqual("abc_def_20260703_123456_789.png", namer.BuildFilename(" abc def "));
        AssertEqual("qws_20260703_123456_789.png", namer.BuildFilename("   "));
        AssertEqual("name.with-dash_01_20260703_123456_789.png", namer.BuildFilename("name.with-dash_01"));
    }

    private static void CaptureServiceUsesNormalizedBackendAndWritesPng()
    {
        using TempDirectory temp = new();
        FakeCaptureBackend wgc = new(CaptureBackendIds.Wgc, Color.Red);
        FakeCaptureBackend dxgi = new(CaptureBackendIds.Dxgi, Color.Green);
        FakeCaptureBackend gdi = new(CaptureBackendIds.Gdi, Color.Blue);
        DisplayInfo display = new(@"\\.\DISPLAY1", new Rectangle(0, 0, 1920, 1080), new Rectangle(0, 0, 1920, 1040), true);
        CaptureTarget target = new(new IntPtr(123), new Rectangle(10, 20, 32, 24), display);
        FixedTargetResolver resolver = new(target);
        FixedFileNamer namer = new("fixed.png");

        using CaptureService service = new([wgc, dxgi, gdi], resolver, namer);
        CaptureResult result = service.Capture(new CaptureRequest(
            new IntPtr(999),
            temp.Path,
            "ignored",
            "winrt",
            false,
            CaptureTargetModes.WindowContent));

        AssertEqual(Path.Combine(temp.Path, "fixed.png"), result.Path);
        AssertEqual(CaptureBackendIds.Wgc, result.Backend);
        AssertEqual(target.ContentRect, result.Rect);
        AssertEqual(target.ContentRect.Size, result.Size);
        AssertTrue(File.Exists(result.Path));
        AssertEqual(1, wgc.CaptureCount);
        AssertEqual(0, dxgi.CaptureCount);
        AssertEqual(0, gdi.CaptureCount);
        AssertEqual(target.Hwnd, wgc.LastRequest?.Hwnd);
        AssertEqual(target.ContentRect, wgc.LastRequest?.ContentRect);

        using Bitmap bitmap = new(result.Path);
        AssertEqual(target.ContentRect.Width, bitmap.Width);
        AssertEqual(target.ContentRect.Height, bitmap.Height);
    }

    private static void FullscreenTargetFallsBackFromWgcToDxgi()
    {
        using TempDirectory temp = new();
        FakeCaptureBackend wgc = new(CaptureBackendIds.Wgc, Color.Red);
        FakeCaptureBackend dxgi = new(CaptureBackendIds.Dxgi, Color.Green);
        DisplayInfo display = new(@"\\.\DISPLAY1", new Rectangle(0, 0, 1920, 1080), new Rectangle(0, 0, 1920, 1040), true);
        CaptureTarget target = new(new IntPtr(123), new Rectangle(0, 0, 64, 36), display);
        FixedTargetResolver resolver = new(target);
        FixedFileNamer namer = new("fullscreen.png");

        using CaptureService service = new([wgc, dxgi], resolver, namer);
        CaptureResult result = service.Capture(new CaptureRequest(
            IntPtr.Zero,
            temp.Path,
            "ignored",
            CaptureBackendIds.Wgc,
            false,
            CaptureTargetModes.FullScreenTarget));

        AssertEqual(CaptureBackendIds.Dxgi, result.Backend);
        AssertEqual(0, wgc.CaptureCount);
        AssertEqual(1, dxgi.CaptureCount);
        AssertTrue(File.Exists(result.Path));
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
        }
    }

    private static void AssertTrue(bool value, string? message = null)
    {
        if (!value)
        {
            throw new InvalidOperationException(message ?? "Expected true.");
        }
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }
    }

    private static void AssertContains(string expectedPart, string actual)
    {
        if (!actual.Contains(expectedPart, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected '{actual}' to contain '{expectedPart}'.");
        }
    }

    private static void AssertThrows<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Expected {typeof(T).Name}, got {ex.GetType().Name}.");
        }

        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class FixedTargetResolver(CaptureTarget target) : ICaptureTargetResolver
    {
        public CaptureTarget Resolve(CaptureRequest request) => target;
    }

    private sealed class FixedFileNamer(string filename) : ICaptureFileNamer
    {
        public string BuildFilename(string prefix) => filename;
    }

    private sealed class FakeCaptureBackend(string id, Color color) : ICaptureBackend
    {
        public string Id { get; } = id;

        public int CaptureCount { get; private set; }

        public CaptureBackendRequest? LastRequest { get; private set; }

        public Bitmap Capture(CaptureBackendRequest request)
        {
            CaptureCount++;
            LastRequest = request;
            Bitmap bitmap = new(request.ContentRect.Width, request.ContentRect.Height);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(color);
            return bitmap;
        }

        public void Dispose()
        {
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QuickWindowScreenshot.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
