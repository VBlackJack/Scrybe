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
/// An immutable, in-memory captured frame in physical pixels, decoupled from any UI type.
/// Holds the raw BGRA pixel buffer plus the geometry and DPI needed to render an overlay and
/// map device-independent selection coordinates back to physical pixels.
/// </summary>
/// <param name="PixelsBgra">Tightly packed BGRA pixels (stride = <paramref name="Width"/> × 4).</param>
/// <param name="Width">Frame width in physical pixels.</param>
/// <param name="Height">Frame height in physical pixels.</param>
/// <param name="DpiX">Horizontal DPI of the source monitor (96 at 100% scaling).</param>
/// <param name="DpiY">Vertical DPI of the source monitor (96 at 100% scaling).</param>
/// <param name="MonitorLeft">Left edge of the source monitor in physical pixels.</param>
/// <param name="MonitorTop">Top edge of the source monitor in physical pixels.</param>
public sealed record CapturedFrame(
    byte[] PixelsBgra,
    int Width,
    int Height,
    double DpiX,
    double DpiY,
    int MonitorLeft,
    int MonitorTop)
{
    /// <summary>The horizontal DPI scale factor (for example 1.5 at 150% scaling).</summary>
    public double DpiScale => DpiX / AppConstants.DipBaseline;
}
