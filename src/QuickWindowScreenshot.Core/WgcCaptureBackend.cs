using System.Drawing.Imaging;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace QuickWindowScreenshot;

internal sealed class WgcCaptureBackend : ICaptureBackend
{
    private readonly object _lock = new();
    private WgcD3DDevice? _device;
    private bool _disposed;

    public string Id => CaptureBackendIds.Wgc;

    public Bitmap Capture(CaptureBackendRequest request) => CaptureWindow(request.Hwnd, request.ContentRect.Size);

    public Bitmap CaptureWindow(IntPtr hwnd, Size expectedSize)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            WgcD3DDevice device = EnsureDevice();

            GraphicsCaptureItem item = WgcInterop.CreateItemForWindow(hwnd);
            if (item.Size.Width <= 0 || item.Size.Height <= 0)
            {
                throw new InvalidOperationException("WGC 返回的窗口尺寸无效");
            }

            Size captureSize = new(item.Size.Width, item.Size.Height);
            using Direct3D11CaptureFramePool framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                device.WinRtDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                item.Size);
            using GraphicsCaptureSession session = framePool.CreateCaptureSession(item);
            using ManualResetEventSlim frameReady = new(false);
            using ManualResetEventSlim itemClosed = new(false);

            framePool.FrameArrived += (_, _) => frameReady.Set();
            item.Closed += (_, _) => itemClosed.Set();
            session.StartCapture();

            if (!WaitForFrame(frameReady, itemClosed, TimeSpan.FromSeconds(2)))
            {
                throw new InvalidOperationException("WGC 没有返回画面，请确认目标窗口可见且允许捕获");
            }

            using Direct3D11CaptureFrame frame = framePool.TryGetNextFrame()
                ?? throw new InvalidOperationException("WGC frame 为空");
            captureSize = new(frame.ContentSize.Width, frame.ContentSize.Height);
            if (captureSize.Width <= 0 || captureSize.Height <= 0)
            {
                throw new InvalidOperationException("WGC frame 尺寸无效");
            }

            Bitmap bitmap = device.CopySurfaceToBitmap(frame.Surface, captureSize);
            if (bitmap.Size == expectedSize)
            {
                return bitmap;
            }

            using (bitmap)
            {
                if (expectedSize.Width <= 0 || expectedSize.Height <= 0)
                {
                    return (Bitmap)bitmap.Clone();
                }

                Rectangle crop = new(0, 0, Math.Min(expectedSize.Width, bitmap.Width), Math.Min(expectedSize.Height, bitmap.Height));
                return bitmap.Clone(crop, PixelFormat.Format32bppRgb);
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

            _device?.Dispose();
            _device = null;
            _disposed = true;
        }
    }

    private WgcD3DDevice EnsureDevice() => _device ??= WgcD3DDevice.Create();

    private static bool WaitForFrame(ManualResetEventSlim frameReady, ManualResetEventSlim itemClosed, TimeSpan timeout)
    {
        WaitHandle[] handles = [frameReady.WaitHandle, itemClosed.WaitHandle];
        int index = WaitHandle.WaitAny(handles, timeout);
        return index == 0;
    }
}
