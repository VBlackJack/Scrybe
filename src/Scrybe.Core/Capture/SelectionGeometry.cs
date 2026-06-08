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

using Scrybe.Core.Models;

namespace Scrybe.Core.Capture;

/// <summary>
/// Pure, UI-free conversion from an overlay drag (two points in device-independent pixels) to a
/// physical-pixel crop rectangle. Handles drag direction normalization, DPI scaling, clamping to
/// the frame bounds and the minimum-size rule. This is the unit-tested producer behind the overlay.
/// </summary>
public static class SelectionGeometry
{
    /// <summary>
    /// Converts a drag from <paramref name="startXDip"/>,<paramref name="startYDip"/> to
    /// <paramref name="endXDip"/>,<paramref name="endYDip"/> (device-independent pixels, overlay-relative)
    /// into a physical-pixel crop rectangle.
    /// </summary>
    /// <param name="startXDip">Drag start X in DIPs.</param>
    /// <param name="startYDip">Drag start Y in DIPs.</param>
    /// <param name="endXDip">Drag end X in DIPs.</param>
    /// <param name="endYDip">Drag end Y in DIPs.</param>
    /// <param name="dpiScale">DPI scale factor (for example 1.5 at 150%).</param>
    /// <param name="frameWidth">Frame width in physical pixels (clamp upper bound for X).</param>
    /// <param name="frameHeight">Frame height in physical pixels (clamp upper bound for Y).</param>
    /// <param name="minPhysicalSize">Minimum width and height, in physical pixels, for a valid selection.</param>
    /// <returns>The clamped, normalized crop rectangle, or <see langword="null"/> if it is below the minimum size.</returns>
    public static PixelRect? ToPhysicalCrop(
        double startXDip,
        double startYDip,
        double endXDip,
        double endYDip,
        double dpiScale,
        int frameWidth,
        int frameHeight,
        int minPhysicalSize)
    {
        double leftDip = Math.Min(startXDip, endXDip);
        double topDip = Math.Min(startYDip, endYDip);
        double rightDip = Math.Max(startXDip, endXDip);
        double bottomDip = Math.Max(startYDip, endYDip);

        int left = ClampToBound((int)Math.Round(leftDip * dpiScale), frameWidth);
        int top = ClampToBound((int)Math.Round(topDip * dpiScale), frameHeight);
        int right = ClampToBound((int)Math.Round(rightDip * dpiScale), frameWidth);
        int bottom = ClampToBound((int)Math.Round(bottomDip * dpiScale), frameHeight);

        int width = right - left;
        int height = bottom - top;

        if (width < minPhysicalSize || height < minPhysicalSize)
        {
            return null;
        }

        return new PixelRect(left, top, width, height);
    }

    private static int ClampToBound(int value, int upperBound) => Math.Clamp(value, 0, upperBound);
}
