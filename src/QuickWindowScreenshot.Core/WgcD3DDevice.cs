using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using WinRT;

namespace QuickWindowScreenshot;

internal sealed class WgcD3DDevice : IDisposable
{
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private bool _disposed;

    private WgcD3DDevice(ID3D11Device device, ID3D11DeviceContext context, IDirect3DDevice winRtDevice)
    {
        _device = device;
        _context = context;
        WinRtDevice = winRtDevice;
    }

    public IDirect3DDevice WinRtDevice { get; }

    public static WgcD3DDevice Create()
    {
        ID3D11Device? device = null;
        ID3D11DeviceContext? context = null;
        IDirect3DDevice? winRtDevice = null;

        try
        {
            D3D11.D3D11CreateDevice(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0],
                out device,
                out context).CheckError();

            using IDXGIDevice dxgiDevice = device.QueryInterface<IDXGIDevice>();
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr inspectable));
            try
            {
                winRtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
            }
            finally
            {
                Marshal.Release(inspectable);
            }

            WgcD3DDevice result = new(device, context, winRtDevice);
            device = null;
            context = null;
            winRtDevice = null;
            return result;
        }
        finally
        {
            winRtDevice?.Dispose();
            context?.Dispose();
            device?.Dispose();
        }
    }

    public Bitmap CopySurfaceToBitmap(IDirect3DSurface surface, Size size)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using ID3D11Texture2D texture = WgcInterop.GetTextureFromSurface(surface);
        return CopyTextureToBitmap(texture, size);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        WinRtDevice.Dispose();
        _context.Dispose();
        _device.Dispose();
        _disposed = true;
    }

    private Bitmap CopyTextureToBitmap(ID3D11Texture2D source, Size size)
    {
        Texture2DDescription description = source.Description;
        description.Width = size.Width;
        description.Height = size.Height;
        description.MipLevels = 1;
        description.ArraySize = 1;
        description.SampleDescription = new SampleDescription(1, 0);
        description.Usage = ResourceUsage.Staging;
        description.BindFlags = BindFlags.None;
        description.CPUAccessFlags = CpuAccessFlags.Read;
        description.MiscFlags = ResourceOptionFlags.None;

        using ID3D11Texture2D staging = _device.CreateTexture2D(description);
        _context.CopyResource(staging, source);
        MappedSubresource mapped = _context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            Bitmap bitmap = new(size.Width, size.Height, PixelFormat.Format32bppRgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                int rowBytes = size.Width * 4;
                for (int y = 0; y < size.Height; y++)
                {
                    IntPtr sourceRow = IntPtr.Add(mapped.DataPointer, y * mapped.RowPitch);
                    IntPtr destinationRow = IntPtr.Add(data.Scan0, y * data.Stride);
                    byte[] row = new byte[rowBytes];
                    Marshal.Copy(sourceRow, row, 0, rowBytes);
                    Marshal.Copy(row, 0, destinationRow, rowBytes);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }
        finally
        {
            _context.Unmap(staging, 0);
        }
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
}
