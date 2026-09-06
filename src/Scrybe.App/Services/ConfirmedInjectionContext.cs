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

using Scrybe.Core.Interfaces;

namespace Scrybe.App.Services;

/// <summary>Revalidates confirmed window identity, foreground and layout before each input batch.</summary>
public sealed class ConfirmedInjectionContext : IInjectionContext
{
    private readonly ITargetWindowGateway _gateway;
    private readonly TargetWindowInfo _target;
    private bool _invalidated;

    /// <summary>Captures a confirmed target's layout.</summary>
    /// <param name="gateway">Window and input-layout gateway.</param>
    /// <param name="target">Identity that was shown in the confirmation prompt.</param>
    public ConfirmedInjectionContext(ITargetWindowGateway gateway, TargetWindowInfo target, Scrybe.Core.Models.InjectionProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(target);
        _gateway = gateway;
        _target = target;
        Profile = profile;
        KeyboardLayout = gateway.GetKeyboardLayout(target.Hwnd);
    }

    /// <inheritdoc />
    public IntPtr KeyboardLayout { get; }

    /// <inheritdoc />
    public Scrybe.Core.Models.InjectionProfile? Profile { get; }

    /// <inheritdoc />
    public string TargetDisplay => $"{_target.ProcessName} ({_target.ProcessId}, 0x{_target.Hwnd.ToInt64():X})";

    /// <inheritdoc />
    public bool IsCurrent
    {
        get
        {
            bool valid = !_invalidated && KeyboardLayout != IntPtr.Zero
                && _gateway.GetForegroundWindow() == _target.Hwnd
                && _gateway.TryGetInfo(_target.Hwnd, out TargetWindowInfo? current)
                && current is not null && current.Hwnd == _target.Hwnd
                && current.ProcessId == _target.ProcessId
                && string.Equals(current.ProcessName, _target.ProcessName, StringComparison.Ordinal)
                && _gateway.GetKeyboardLayout(_target.Hwnd) == KeyboardLayout;
            _invalidated |= !valid;
            return valid;
        }
    }
}
