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

namespace Scrybe.Core.Interfaces;

/// <summary>
/// Captures the screen into an in-memory frame. The baseline implementation uses
/// Windows.Graphics.Capture so that hardware-accelerated and DirectX overlay surfaces
/// (common in remote-console viewers) are captured correctly rather than rendered black.
/// </summary>
public interface IScreenCaptureService
{
    /// <summary>
    /// Captures the monitor under the mouse cursor at its physical-pixel resolution into an
    /// in-memory frame.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the capture.</param>
    /// <returns>The captured frame, including pixels, size and DPI.</returns>
    Task<CapturedFrame> CaptureCursorMonitorFrameAsync(CancellationToken cancellationToken = default);

    /// <summary>Eagerly initializes the capture device so the first capture is fast. Safe to call repeatedly.</summary>
    void Prewarm();
}
