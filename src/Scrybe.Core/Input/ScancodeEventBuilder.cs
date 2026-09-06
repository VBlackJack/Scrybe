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

namespace Scrybe.Core.Input;

/// <summary>One scancode key event.</summary>
/// <param name="ScanCode">Hardware scancode.</param>
/// <param name="KeyUp">Whether the key is released.</param>
public sealed record ScancodeKeyEvent(ushort ScanCode, bool KeyUp);

/// <summary>Builds adjacent batches, including Shift release before Tab or Enter.</summary>
public sealed class ScancodeEventBuilder
{
    /// <summary>Whether the preceding batch retained Shift.</summary>
    public bool ShiftHeld { get; private set; }

    /// <summary>Clears state at the start/end of one injection.</summary>
    public void Reset() => ShiftHeld = false;

    /// <summary>Builds a mapped stroke. Special keys always use an unshifted state.</summary>
    /// <param name="stroke">Logical key intent.</param>
    /// <param name="scanCode">Resolved printable character scancode.</param>
    /// <param name="requiresShift">Resolved character shift requirement.</param>
    public IReadOnlyList<ScancodeKeyEvent> Build(KeyStroke stroke, ushort scanCode, bool requiresShift)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        if (stroke.IsSpecial)
        {
            scanCode = stroke.Special == SpecialKey.Enter ? AppConstants.EnterScanCode : AppConstants.TabScanCode;
            requiresShift = false;
        }

        List<ScancodeKeyEvent> events = [];
        ShiftTransition transition = ShiftStateMachine.Next(ShiftHeld, requiresShift);
        if (transition.EmitShiftDown) { events.Add(new(AppConstants.LeftShiftScanCode, false)); }
        if (transition.EmitShiftUp) { events.Add(new(AppConstants.LeftShiftScanCode, true)); }
        ShiftHeld = transition.ShiftHeld;
        events.Add(new(scanCode, false));
        events.Add(new(scanCode, true));
        return events;
    }
}
