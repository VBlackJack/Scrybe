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

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Scrybe.Core.IO;
using Scrybe.Core.Models;

namespace Scrybe.App.Imaging;

/// <summary>
/// Bridges the UI-free <see cref="CapturedFrame"/> to WPF imaging: building a frozen bitmap for
/// the overlay and cropping the selected physical-pixel region to a PNG on disk.
/// </summary>
public static class FrameImaging
{
    private const int BytesPerPixel = 4;
    private const double NominalDpi = 96.0;

    /// <summary>Builds a frozen <see cref="BitmapSource"/> from a captured frame, safe for cross-thread use.</summary>
    /// <param name="frame">The captured frame to wrap.</param>
    public static BitmapSource ToFrozenBitmap(CapturedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        int stride = frame.Width * BytesPerPixel;
        BitmapSource bitmap = BitmapSource.Create(
            frame.Width,
            frame.Height,
            frame.DpiX,
            frame.DpiY,
            PixelFormats.Bgra32,
            null,
            frame.PixelsBgra,
            stride);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Encodes a processed pixel buffer as PNG bytes for the OCR engine.</summary>
    /// <param name="buffer">The BGRA pixel buffer to encode.</param>
    /// <returns>The buffer encoded as a PNG byte array.</returns>
    public static byte[] EncodePixelBuffer(PixelBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        BitmapSource bitmap = BitmapSource.Create(
            buffer.Width,
            buffer.Height,
            NominalDpi,
            NominalDpi,
            PixelFormats.Bgra32,
            null,
            buffer.Bgra,
            buffer.Width * BytesPerPixel);
        bitmap.Freeze();

        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using MemoryStream stream = new();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Crops <paramref name="frozen"/> to the physical-pixel <paramref name="rect"/> and writes a
    /// timestamped PNG into the resolved captures directory.
    /// </summary>
    /// <param name="frozen">The frozen full-frame bitmap.</param>
    /// <param name="rect">The crop rectangle in physical pixels.</param>
    /// <param name="capturesDirectory">Configured captures directory, or <see langword="null"/> for the default.</param>
    /// <returns>The absolute path of the written PNG.</returns>
    public static string SaveCrop(BitmapSource frozen, PixelRect rect, string? capturesDirectory)
    {
        ArgumentNullException.ThrowIfNull(frozen);
        ArgumentNullException.ThrowIfNull(rect);

        CroppedBitmap cropped = new(frozen, new Int32Rect(rect.X, rect.Y, rect.Width, rect.Height));

        string directory = CapturePathResolver.ResolveDirectory(capturesDirectory);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, CapturePathResolver.BuildFileName(DateTime.Now));

        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(cropped));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);

        return path;
    }
}
