using System.Drawing.Imaging;

namespace QuickWindowScreenshot;

internal sealed record CaptureRequest(
    IntPtr Hwnd,
    string OutputDir,
    string Prefix,
    string Backend,
    bool BringToFront,
    string TargetMode);

internal sealed record CaptureResult(string Path, Rectangle Rect, Size Size, string Backend);

internal sealed class CaptureService : IDisposable
{
    private readonly object _sync = new();
    private readonly IReadOnlyDictionary<string, ICaptureBackend> _backends;
    private readonly ICaptureTargetResolver _targetResolver;
    private readonly ICaptureFileNamer _fileNamer;
    private bool _disposed;

    public CaptureService()
        : this(
            [new WgcCaptureBackend(), new DxgiCaptureBackend(), new GdiCaptureBackend()],
            new WindowCaptureTargetResolver(),
            new TimestampedCaptureFileNamer())
    {
    }

    internal CaptureService(IEnumerable<ICaptureBackend> backends)
        : this(backends, new WindowCaptureTargetResolver(), new TimestampedCaptureFileNamer())
    {
    }

    internal CaptureService(
        IEnumerable<ICaptureBackend> backends,
        ICaptureTargetResolver targetResolver,
        ICaptureFileNamer fileNamer)
    {
        _backends = backends.ToDictionary(backend => backend.Id, StringComparer.OrdinalIgnoreCase);
        _targetResolver = targetResolver;
        _fileNamer = fileNamer;
    }

    public CaptureResult Capture(CaptureRequest request)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            CaptureTarget target = _targetResolver.Resolve(request);
            Directory.CreateDirectory(request.OutputDir);
            string path = Path.Combine(request.OutputDir, _fileNamer.BuildFilename(request.Prefix));
            string backendId = NormalizeBackend(request.Backend);
            if (CaptureTargetModes.Normalize(request.TargetMode) == CaptureTargetModes.FullScreenTarget
                && backendId == CaptureBackendIds.Wgc)
            {
                backendId = CaptureBackendIds.Dxgi;
            }
            ICaptureBackend backend = ResolveBackend(backendId);
            using Bitmap bitmap = backend.Capture(new CaptureBackendRequest(target.Hwnd, target.ContentRect, target.Display));
            bitmap.Save(path, ImageFormat.Png);

            return new CaptureResult(path, target.ContentRect, target.ContentRect.Size, backendId);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            foreach (ICaptureBackend backend in _backends.Values)
            {
                backend.Dispose();
            }
            _disposed = true;
        }
    }

    private ICaptureBackend ResolveBackend(string backendId) =>
        _backends.TryGetValue(backendId, out ICaptureBackend? backend)
            ? backend
            : throw new InvalidOperationException($"截图后端不可用: {backendId}");

    internal static string NormalizeBackend(string backend) => CaptureBackendIds.Normalize(backend);
}
