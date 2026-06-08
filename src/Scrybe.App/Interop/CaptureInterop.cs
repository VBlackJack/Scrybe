/*
 * Copyright 2026 Julien Bombled
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace Scrybe.App.Interop;

/// <summary>
/// Low-level interop bridging Windows.Graphics.Capture (WinRT) with a Vortice/Direct3D11
/// device. Concentrates every unsafe COM / P/Invoke detail in one place so the capture
/// service can stay readable.
/// </summary>
internal static class CaptureInterop
{
    private const uint MonitorDefaultToPrimary = 0x00000001;
    private const int MonitorDpiTypeEffective = 0;
    private const uint DefaultDpi = 96;

    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid Texture2DIid = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    /// <summary>Holds the Direct3D11 device objects required to drive a capture session.</summary>
    internal sealed record CaptureDevice(
        ID3D11Device Device,
        ID3D11DeviceContext Context,
        IDirect3DDevice WinRtDevice);

    /// <summary>Returns the handle of the primary monitor.</summary>
    public static IntPtr GetPrimaryMonitor()
    {
        NativePoint origin = new(0, 0);
        return MonitorFromPoint(origin, MonitorDefaultToPrimary);
    }

    /// <summary>Returns the effective DPI of the given monitor, falling back to 96 on failure.</summary>
    /// <param name="monitor">The monitor handle.</param>
    public static (uint DpiX, uint DpiY) GetMonitorDpi(IntPtr monitor)
    {
        int hr = GetDpiForMonitor(monitor, MonitorDpiTypeEffective, out uint dpiX, out uint dpiY);
        return hr == 0 ? (dpiX, dpiY) : (DefaultDpi, DefaultDpi);
    }

    /// <summary>Returns the bounds of the given monitor in physical pixels.</summary>
    /// <param name="monitor">The monitor handle.</param>
    public static (int Left, int Top, int Width, int Height) GetMonitorBounds(IntPtr monitor)
    {
        MonitorInfo info = new()
        {
            Size = (uint)Marshal.SizeOf<MonitorInfo>(),
        };

        if (!GetMonitorInfo(monitor, ref info))
        {
            return (0, 0, 0, 0);
        }

        NativeRect bounds = info.Monitor;
        return (bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
    }

    /// <summary>Creates a hardware Direct3D11 device and its WinRT projection for capture.</summary>
    public static CaptureDevice CreateCaptureDevice()
    {
        FeatureLevel[] featureLevels =
        {
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
        };

        D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            featureLevels,
            out ID3D11Device device,
            out FeatureLevel _,
            out ID3D11DeviceContext context).CheckError();

        using IDXGIDevice dxgiDevice = device.QueryInterface<IDXGIDevice>();
        int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr inspectable);
        if (hr != 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        try
        {
            IDirect3DDevice winrtDevice = MarshalInspectable<IDirect3DDevice>.FromAbi(inspectable);
            return new CaptureDevice(device, context, winrtDevice);
        }
        finally
        {
            Marshal.Release(inspectable);
        }
    }

    /// <summary>Creates a capture item that targets an entire monitor.</summary>
    /// <param name="monitor">The monitor handle to capture.</param>
    public static GraphicsCaptureItem CreateCaptureItemForMonitor(IntPtr monitor)
    {
        IGraphicsCaptureItemInterop interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        Guid iid = GraphicsCaptureItemIid;
        IntPtr itemPointer = interop.CreateForMonitor(monitor, ref iid);

        try
        {
            return GraphicsCaptureItem.FromAbi(itemPointer);
        }
        finally
        {
            Marshal.Release(itemPointer);
        }
    }

    /// <summary>Extracts the underlying Direct3D11 texture from a captured WinRT surface.</summary>
    /// <param name="surface">The surface carried by a captured frame.</param>
    public static ID3D11Texture2D GetTexture2D(IDirect3DSurface surface)
    {
        IDirect3DDxgiInterfaceAccess access = surface.As<IDirect3DDxgiInterfaceAccess>();
        Guid iid = Texture2DIid;
        IntPtr texturePointer = access.GetInterface(ref iid);
        return new ID3D11Texture2D(texturePointer);
    }

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }
}

/// <summary>
/// Factory interop for <see cref="GraphicsCaptureItem"/>, used to build capture items from a
/// window or monitor handle. Mirrors <c>IGraphicsCaptureItemInterop</c> from the Windows SDK.
/// </summary>
[ComImport]
[Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IGraphicsCaptureItemInterop
{
    /// <summary>Creates a capture item for a window handle.</summary>
    IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);

    /// <summary>Creates a capture item for a monitor handle.</summary>
    IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
}

/// <summary>
/// Provides access to the native DXGI interface behind a WinRT Direct3D surface. Mirrors
/// <c>IDirect3DDxgiInterfaceAccess</c> from the Windows SDK.
/// </summary>
[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDirect3DDxgiInterfaceAccess
{
    /// <summary>Returns the requested native interface pointer for the surface.</summary>
    IntPtr GetInterface([In] ref Guid iid);
}
