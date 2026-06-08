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

namespace Scrybe.Core.Models;

/// <summary>
/// An in-memory, UI-free image: tightly packed BGRA pixels (stride = <paramref name="Width"/> × 4)
/// with its dimensions. Used as the unit of exchange for the pure pre-processing pipeline.
/// </summary>
/// <param name="Bgra">Tightly packed BGRA pixels.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public sealed record PixelBuffer(byte[] Bgra, int Width, int Height)
{
    private const int BytesPerPixel = 4;

    /// <summary>Extracts a sub-rectangle of a captured frame into a new, tightly packed buffer.</summary>
    /// <param name="frame">The source frame.</param>
    /// <param name="rect">The region to extract, in physical pixels, assumed within the frame bounds.</param>
    public static PixelBuffer FromFrameRegion(CapturedFrame frame, PixelRect rect)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(rect);

        int sourceStride = frame.Width * BytesPerPixel;
        int destinationStride = rect.Width * BytesPerPixel;
        byte[] destination = new byte[destinationStride * rect.Height];

        for (int row = 0; row < rect.Height; row++)
        {
            int sourceOffset = ((rect.Y + row) * sourceStride) + (rect.X * BytesPerPixel);
            Array.Copy(frame.PixelsBgra, sourceOffset, destination, row * destinationStride, destinationStride);
        }

        return new PixelBuffer(destination, rect.Width, rect.Height);
    }
}
