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

using Scrybe.App.Interop;

namespace Scrybe.App.Services;

/// <summary>Production target-window gateway backed by Win32 interop.</summary>
public sealed class InteropTargetWindowGateway : ITargetWindowGateway
{
    /// <inheritdoc />
    public IntPtr GetForegroundWindow() => InjectionInterop.GetForegroundWindowHandle();

    /// <inheritdoc />
    public bool TryGetInfo(IntPtr window, out TargetWindowInfo? target)
    {
        target = null;
        if (!InjectionInterop.TryGetTargetInfo(window, out InjectionInterop.TargetInfo? interopTarget)
            || interopTarget is null)
        {
            return false;
        }

        target = new TargetWindowInfo(
            interopTarget.Hwnd,
            interopTarget.Title,
            interopTarget.ProcessName,
            interopTarget.ProcessId);
        return true;
    }

    /// <inheritdoc />
    public bool TryRestore(IntPtr window) => InjectionInterop.TryRestoreForeground(window);
}
