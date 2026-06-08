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
using Scrybe.Core.Logging;

namespace Scrybe.App.Interop;

/// <summary>Interop for native window chrome attributes.</summary>
internal static class WindowChromeInterop
{
    private const int SuccessHresult = 0;
    private const int DarkTitleBarEnabled = 1;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    /// <summary>Applies Windows DWM immersive dark mode to the native title bar.</summary>
    /// <param name="hwnd">Window handle whose title bar should be darkened.</param>
    public static void ApplyDarkTitleBar(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            int enabled = DarkTitleBarEnabled;
            int result = DwmSetWindowAttribute(
                hwnd,
                DwmwaUseImmersiveDarkMode,
                ref enabled,
                Marshal.SizeOf<int>());

            if (result == SuccessHresult)
            {
                return;
            }

            enabled = DarkTitleBarEnabled;
            int legacyResult = DwmSetWindowAttribute(
                hwnd,
                DwmwaUseImmersiveDarkModeBefore20H1,
                ref enabled,
                Marshal.SizeOf<int>());

            if (legacyResult != SuccessHresult)
            {
                FileLogger.Info(
                    $"Dark title bar DWM attribute failed: current HRESULT {result}, legacy HRESULT {legacyResult}.");
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            FileLogger.Warn($"Dark title bar DWM attribute unavailable: {exception.Message}");
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
