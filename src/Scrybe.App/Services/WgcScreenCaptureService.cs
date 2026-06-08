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
using Scrybe.App.Interop;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Vortice.Direct3D11;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace Scrybe.App.Services;

/// <summary>
/// Captures the primary monitor into an in-memory <see cref="CapturedFrame"/> with
/// Windows.Graphics.Capture and a Vortice Direct3D11 device. The device is created once and kept
/// warm across captures so the hotkey-to-overlay latency stays low; the frame pool and session are
/// lightweight and created per shot. Disposing the service releases the warm device.
/// </summary>
public sealed class WgcScreenCaptureService : IScreenCaptureService, IDisposable
{
    private const int FramePoolBufferCount = 2;
    private const int CaptureTimeoutMs = 5000;
    private const int BytesPerPixel = 4;

    private readonly Lock _deviceGate = new();

    private CaptureInterop.CaptureDevice? _warmDevice;
    private bool _isDisposed;

    /// <inheritdoc />
    public async Task<CapturedFrame> CapturePrimaryMonitorFrameAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!GraphicsCaptureSession.IsSupported())
        {
            throw new NotSupportedException("Windows.Graphics.Capture is not supported on this system.");
        }

        CaptureInterop.CaptureDevice device = EnsureWarmDevice();

        IntPtr monitor = CaptureInterop.GetPrimaryMonitor();
        (uint dpiX, uint dpiY) = CaptureInterop.GetMonitorDpi(monitor);
        (int monitorLeft, int monitorTop, int _, int _) = CaptureInterop.GetMonitorBounds(monitor);
        GraphicsCaptureItem item = CaptureInterop.CreateCaptureItemForMonitor(monitor);

        Direct3D11CaptureFramePool? framePool = null;
        GraphicsCaptureSession? session = null;
        TypedEventHandler<Direct3D11CaptureFramePool, object>? handler = null;

        try
        {
            framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                device.WinRtDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                FramePoolBufferCount,
                item.Size);

            TaskCompletionSource<Direct3D11CaptureFrame> frameSource =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            handler = (pool, _) =>
            {
                Direct3D11CaptureFrame? arrived = pool.TryGetNextFrame();
                if (arrived is not null)
                {
                    frameSource.TrySetResult(arrived);
                }
            };
            framePool.FrameArrived += handler;

            session = framePool.CreateCaptureSession(item);
            session.StartCapture();

            using CancellationTokenSource timeoutSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(CaptureTimeoutMs);

            await using (timeoutSource.Token.Register(static state =>
                ((TaskCompletionSource<Direct3D11CaptureFrame>)state!).TrySetCanceled(), frameSource))
            {
                using Direct3D11CaptureFrame frame = await frameSource.Task.ConfigureAwait(false);
                return ReadFrame(device, frame, dpiX, dpiY, monitorLeft, monitorTop);
            }
        }
        finally
        {
            if (handler is not null && framePool is not null)
            {
                framePool.FrameArrived -= handler;
            }

            session?.Dispose();
            framePool?.Dispose();
        }
    }

    /// <inheritdoc />
    public void Prewarm()
    {
        if (_isDisposed)
        {
            return;
        }

        EnsureWarmDevice();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_deviceGate)
        {
            if (_warmDevice is not null)
            {
                _warmDevice.WinRtDevice.Dispose();
                _warmDevice.Context.Dispose();
                _warmDevice.Device.Dispose();
                _warmDevice = null;
            }

            _isDisposed = true;
        }
    }

    private CaptureInterop.CaptureDevice EnsureWarmDevice()
    {
        lock (_deviceGate)
        {
            _warmDevice ??= CaptureInterop.CreateCaptureDevice();
            return _warmDevice;
        }
    }

    private static CapturedFrame ReadFrame(
        CaptureInterop.CaptureDevice device,
        Direct3D11CaptureFrame frame,
        uint dpiX,
        uint dpiY,
        int monitorLeft,
        int monitorTop)
    {
        using ID3D11Texture2D gpuTexture = CaptureInterop.GetTexture2D(frame.Surface);
        Texture2DDescription description = gpuTexture.Description;
        int width = (int)description.Width;
        int height = (int)description.Height;

        Texture2DDescription stagingDescription = description;
        stagingDescription.Usage = ResourceUsage.Staging;
        stagingDescription.BindFlags = BindFlags.None;
        stagingDescription.CPUAccessFlags = CpuAccessFlags.Read;
        stagingDescription.MiscFlags = ResourceOptionFlags.None;

        using ID3D11Texture2D staging = device.Device.CreateTexture2D(stagingDescription);
        device.Context.CopyResource(staging, gpuTexture);

        MappedSubresource mapped = device.Context.Map(staging, 0, MapMode.Read, MapFlags.None);
        try
        {
            byte[] pixels = CopyTightBgra(in mapped, width, height);
            return new CapturedFrame(pixels, width, height, dpiX, dpiY, monitorLeft, monitorTop);
        }
        finally
        {
            device.Context.Unmap(staging, 0);
        }
    }

    private static byte[] CopyTightBgra(in MappedSubresource mapped, int width, int height)
    {
        int stride = width * BytesPerPixel;
        byte[] buffer = new byte[stride * height];

        for (int row = 0; row < height; row++)
        {
            IntPtr sourceRow = mapped.DataPointer + (row * (int)mapped.RowPitch);
            Marshal.Copy(sourceRow, buffer, row * stride, stride);
        }

        return buffer;
    }
}
