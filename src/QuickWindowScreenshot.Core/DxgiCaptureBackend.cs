using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using HPPH;
using ScreenCapture.NET;

namespace QuickWindowScreenshot;

internal sealed class DxgiCaptureBackend : ICaptureBackend
{
    private readonly object _lock = new();
    private readonly Dictionary<string, DX11ScreenCapture> _captures = [];
    private DX11ScreenCaptureService? _service;
    private List<Display>? _displays;
    private bool _disposed;

    public string Id => CaptureBackendIds.Dxgi;

    public Bitmap Capture(CaptureBackendRequest request)
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            Display display = FindDisplay(GetService(), request.Display);
            DX11ScreenCapture capture = GetCapture(display);
            CaptureZone<ColorBGRA>? zone = null;

            try
            {
                Rectangle rect = request.ContentRect;
                int x = rect.Left - request.Display.Bounds.Left;
                int y = rect.Top - request.Display.Bounds.Top;
                zone = capture.RegisterCaptureZone(x, y, rect.Width, rect.Height, 0);
                zone.AutoUpdate = true;

                bool captured = false;
                for (int attempt = 0; attempt < 3 && !captured; attempt++)
                {
                    captured = capture.CaptureScreen();
                    if (!captured)
                    {
                        Thread.Sleep(30);
                    }
                }

                if (!captured)
                {
                    throw new InvalidOperationException("DXGI 没有返回画面，请确认目标窗口可见，或切换到 GDI 后端");
                }

                using IDisposable _ = zone.Lock();
                return CreateBitmapFromBgra(zone);
            }
            finally
            {
                if (zone is not null)
                {
                    capture.UnregisterCaptureZone(zone);
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            foreach (DX11ScreenCapture capture in _captures.Values)
            {
                if (capture is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _captures.Clear();
            _displays = null;
            _service?.Dispose();
            _service = null;
            _disposed = true;
        }
    }

    private DX11ScreenCaptureService GetService()
    {
        ThrowIfDisposed();
        _service ??= new DX11ScreenCaptureService();
        return _service;
    }

    private IReadOnlyList<Display> GetDisplays(DX11ScreenCaptureService service)
    {
        _displays ??= service
            .GetGraphicsCards()
            .SelectMany(service.GetDisplays)
            .ToList();
        return _displays;
    }

    private DX11ScreenCapture GetCapture(Display display)
    {
        ThrowIfDisposed();
        string key = DisplayKey(display);
        if (!_captures.TryGetValue(key, out DX11ScreenCapture? capture))
        {
            capture = GetService().GetScreenCapture(display);
            _captures[key] = capture;
        }

        return capture;
    }

    private Display FindDisplay(DX11ScreenCaptureService service, DisplayInfo displayInfo)
    {
        IReadOnlyList<Display> displays = GetDisplays(service);

        Display? exact = displays
            .Cast<Display?>()
            .FirstOrDefault(display => string.Equals(display!.Value.DeviceName, displayInfo.DeviceName, StringComparison.OrdinalIgnoreCase));
        if (exact.HasValue)
        {
            return exact.Value;
        }

        List<Display> sizeMatches = displays
            .Where(display => display.Width == displayInfo.Bounds.Width && display.Height == displayInfo.Bounds.Height)
            .ToList();
        if (sizeMatches.Count == 1)
        {
            return sizeMatches[0];
        }

        string known = string.Join(", ", displays.Select(display => $"{display.DeviceName} {display.Width}x{display.Height}"));
        throw new InvalidOperationException(
            $"无法匹配 DXGI 显示器: {displayInfo.DeviceName} {displayInfo.Bounds.Width}x{displayInfo.Bounds.Height}; 已发现: {known}");
    }

    private static string DisplayKey(Display display) => $"{display.DeviceName}|{display.Width}x{display.Height}";

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static Bitmap CreateBitmapFromBgra(ICaptureZone zone)
    {
        Bitmap bitmap = new(zone.Width, zone.Height, PixelFormat.Format32bppRgb);
        BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.WriteOnly,
            bitmap.PixelFormat);

        try
        {
            byte[] raw = zone.RawBuffer.ToArray();
            int sourceStride = zone.Stride;
            int rowBytes = zone.Width * 4;

            for (int y = 0; y < zone.Height; y++)
            {
                IntPtr destination = IntPtr.Add(data.Scan0, y * data.Stride);
                Marshal.Copy(raw, y * sourceStride, destination, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }
}
