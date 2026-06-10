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
using Scrybe.Core;
using Scrybe.Core.Input;
using Scrybe.Core.Models;

namespace Scrybe.App.Services;

/// <summary>
/// Scancode injection strategy: resolves each character to a hardware scancode against the active
/// keyboard layout and sends it via <c>SendInput</c>. Shift is driven by a state machine (held across
/// consecutive shifted characters, released before an unshifted one) and every character's events -
/// including any Shift transition - are sent in one atomic batch to avoid dropped/bled keystrokes.
/// </summary>
public sealed class ScancodeInjector : KeystrokeInjectorBase
{
    private readonly List<InjectionInterop.KeyEvent> _events = new(4);
    private bool _shiftHeld;

    /// <summary>Initializes the scancode injector.</summary>
    /// <param name="settings">Application settings holding the keystroke delays.</param>
    public ScancodeInjector(AppSettings settings)
        : base(settings)
    {
    }

    /// <inheritdoc />
    protected override void BeginInjection() => _shiftHeld = false;

    /// <inheritdoc />
    protected override void EndInjection()
    {
        if (_shiftHeld)
        {
            InjectionInterop.SendScanCode(AppConstants.LeftShiftScanCode, keyUp: true);
            _shiftHeld = false;
        }
    }

    /// <inheritdoc />
    protected override bool CanRepresentStroke(KeyStroke stroke)
        => stroke.IsSpecial || InjectionInterop.TryResolveScanCode(stroke.Character, out _, out _);

    /// <inheritdoc />
    protected override StrokeResult SendStroke(KeyStroke stroke)
    {
        _events.Clear();

        if (stroke.IsSpecial)
        {
            ushort scanCode = stroke.Special == SpecialKey.Enter ? AppConstants.EnterScanCode : AppConstants.TabScanCode;
            _events.Add(new InjectionInterop.KeyEvent(scanCode, KeyUp: false));
            _events.Add(new InjectionInterop.KeyEvent(scanCode, KeyUp: true));
        }
        else
        {
            if (!InjectionInterop.TryResolveScanCode(stroke.Character, out ushort scanCode, out bool requiresShift))
            {
                return StrokeResult.Skipped;
            }

            ShiftTransition transition = ShiftStateMachine.Next(_shiftHeld, requiresShift);
            if (transition.EmitShiftDown)
            {
                _events.Add(new InjectionInterop.KeyEvent(AppConstants.LeftShiftScanCode, KeyUp: false));
            }

            if (transition.EmitShiftUp)
            {
                _events.Add(new InjectionInterop.KeyEvent(AppConstants.LeftShiftScanCode, KeyUp: true));
            }

            _shiftHeld = transition.ShiftHeld;

            _events.Add(new InjectionInterop.KeyEvent(scanCode, KeyUp: false));
            _events.Add(new InjectionInterop.KeyEvent(scanCode, KeyUp: true));
        }

        return InjectionInterop.SendScanCodes(_events) == (uint)_events.Count
            ? StrokeResult.Sent(_events.Count)
            : StrokeResult.Failure;
    }
}
