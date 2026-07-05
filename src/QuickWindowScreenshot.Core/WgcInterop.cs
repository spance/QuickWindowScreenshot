using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Vortice.Direct3D11;
using WinRT;

namespace QuickWindowScreenshot;

internal static class WgcInterop
{
    private static readonly Guid IGraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid ID3D11Texture2DGuid = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    public static GraphicsCaptureItem CreateItemForWindow(IntPtr hwnd)
    {
        IntPtr className = IntPtr.Zero;
        IntPtr factoryPtr = IntPtr.Zero;
        IntPtr itemPtr = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString("Windows.Graphics.Capture.GraphicsCaptureItem", 44, out className));
            Guid interopGuid = typeof(IGraphicsCaptureItemInterop).GUID;
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(className, ref interopGuid, out factoryPtr));
            IGraphicsCaptureItemInterop interop = Marshal.GetObjectForIUnknown(factoryPtr) as IGraphicsCaptureItemInterop
                ?? throw new InvalidOperationException("无法取得 IGraphicsCaptureItemInterop");
            Guid itemGuid = IGraphicsCaptureItemGuid;
            Marshal.ThrowExceptionForHR(interop.CreateForWindow(hwnd, ref itemGuid, out itemPtr));
            return GraphicsCaptureItem.FromAbi(itemPtr);
        }
        finally
        {
            if (itemPtr != IntPtr.Zero)
            {
                Marshal.Release(itemPtr);
            }
            if (factoryPtr != IntPtr.Zero)
            {
                Marshal.Release(factoryPtr);
            }
            if (className != IntPtr.Zero)
            {
                WindowsDeleteString(className);
            }
        }
    }

    public static ID3D11Texture2D GetTextureFromSurface(IDirect3DSurface surface)
    {
        IntPtr surfacePtr = MarshalInterface<IDirect3DSurface>.FromManaged(surface);
        try
        {
            IDirect3DDxgiInterfaceAccess access = Marshal.GetObjectForIUnknown(surfacePtr) as IDirect3DDxgiInterfaceAccess
                ?? throw new InvalidOperationException("无法取得 IDirect3DDxgiInterfaceAccess");
            Guid textureGuid = ID3D11Texture2DGuid;
            Marshal.ThrowExceptionForHR(access.GetInterface(ref textureGuid, out IntPtr texturePtr));
            return new ID3D11Texture2D(texturePtr);
        }
        finally
        {
            Marshal.Release(surfacePtr);
        }
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig]
        int CreateForWindow(IntPtr window, ref Guid iid, out IntPtr result);

        [PreserveSig]
        int CreateForMonitor(IntPtr monitor, ref Guid iid, out IntPtr result);
    }

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        [PreserveSig]
        int GetInterface(ref Guid iid, out IntPtr p);
    }

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

    [DllImport("combase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string sourceString, int length, out IntPtr hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(IntPtr hstring);
}
