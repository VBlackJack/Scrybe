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

namespace Scrybe.Core.Input;

/// <summary>The Shift transition required before typing the next character, and the resulting state.</summary>
/// <param name="EmitShiftDown">Whether to press Shift before the character.</param>
/// <param name="EmitShiftUp">Whether to release Shift before the character.</param>
/// <param name="ShiftHeld">The Shift state after applying this transition.</param>
public readonly record struct ShiftTransition(bool EmitShiftDown, bool EmitShiftUp, bool ShiftHeld);

/// <summary>
/// Pure Shift state machine for scancode injection. It only toggles Shift when the requirement changes,
/// which prevents the shift-bleed seen in 5a where a stray Shift altered an otherwise lowercase run.
/// </summary>
public static class ShiftStateMachine
{
    /// <summary>Computes the Shift transition for the next character.</summary>
    /// <param name="currentlyHeld">Whether Shift is currently held.</param>
    /// <param name="needsShift">Whether the next character requires Shift.</param>
    public static ShiftTransition Next(bool currentlyHeld, bool needsShift)
    {
        if (needsShift && !currentlyHeld)
        {
            return new ShiftTransition(EmitShiftDown: true, EmitShiftUp: false, ShiftHeld: true);
        }

        if (!needsShift && currentlyHeld)
        {
            return new ShiftTransition(EmitShiftDown: false, EmitShiftUp: true, ShiftHeld: false);
        }

        return new ShiftTransition(EmitShiftDown: false, EmitShiftUp: false, ShiftHeld: currentlyHeld);
    }
}
