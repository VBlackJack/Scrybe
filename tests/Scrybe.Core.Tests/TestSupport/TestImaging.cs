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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Scrybe.Core.Models;

namespace Scrybe.Core.Tests.TestSupport;

/// <summary>Image decode/encode helpers for OCR tests, using the same WPF imaging stack as the app.</summary>
internal static class TestImaging
{
    private const int BytesPerPixel = 4;
    private const double NominalDpi = 96.0;

    /// <summary>Decodes PNG bytes into a tightly packed BGRA <see cref="PixelBuffer"/>.</summary>
    public static PixelBuffer Decode(byte[] png)
    {
        using MemoryStream input = new(png);
        BitmapFrame frame = BitmapFrame.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        FormatConvertedBitmap converted = new(frame, PixelFormats.Bgra32, null, 0);

        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * BytesPerPixel;
        byte[] pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        return new PixelBuffer(pixels, width, height);
    }

    /// <summary>Encodes a BGRA <see cref="PixelBuffer"/> as PNG bytes.</summary>
    public static byte[] Encode(PixelBuffer buffer)
    {
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

        using MemoryStream output = new();
        encoder.Save(output);
        return output.ToArray();
    }
}
