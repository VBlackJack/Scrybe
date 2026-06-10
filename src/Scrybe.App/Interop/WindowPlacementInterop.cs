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
using System.Windows;
using System.Windows.Interop;

namespace Scrybe.App.Interop;

/// <summary>
/// Places a WPF window using physical-pixel coordinates. WPF positions windows in
/// device-independent units resolved against the primary monitor's DPI, which mislocates an
/// overlay meant for a different monitor under mixed-DPI setups. Moving the window in physical
/// pixels via SetWindowPos sidesteps that conversion entirely.
/// </summary>
internal static class WindowPlacementInterop
{
    private const uint SwpNoZorder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    /// <summary>
    /// Moves and sizes the given window to the supplied physical-pixel bounds. The window must
    /// already own an HWND (for example after <see cref="Window.SourceInitialized"/>).
    /// </summary>
    /// <param name="window">The window to move; must have an initialized source.</param>
    /// <param name="left">Left edge in physical pixels.</param>
    /// <param name="top">Top edge in physical pixels.</param>
    /// <param name="width">Width in physical pixels.</param>
    /// <param name="height">Height in physical pixels.</param>
    public static void MoveToPhysicalBounds(Window window, int left, int top, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(window);

        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        SetWindowPos(hwnd, IntPtr.Zero, left, top, width, height, SwpNoZorder | SwpNoActivate);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr hwndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
