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
using Scrybe.Core.Input;
using Scrybe.Core.Models;

namespace Scrybe.App.Services;

/// <summary>
/// Unicode injection strategy: sends each character as its UTF-16 code unit via
/// <c>KEYEVENTF_UNICODE</c>, bypassing the keyboard layout entirely (no scancode, Shift or AltGr).
/// Enter and Tab are sent as virtual keys. This structurally avoids the layout/Shift/AltGr error
/// classes that affect scancode injection.
/// </summary>
public sealed class UnicodeInjector : KeystrokeInjectorBase
{
    /// <summary>Initializes the Unicode injector.</summary>
    /// <param name="settings">Application settings holding the keystroke delays.</param>
    public UnicodeInjector(AppSettings settings)
        : base(settings)
    {
    }

    /// <inheritdoc />
    protected override StrokeResult SendStroke(KeyStroke stroke)
    {
        IReadOnlyList<UnicodeKeyEvent> events = UnicodeKeystrokeMapper.Map(stroke);
        return InjectionInterop.SendUnicodeKeyEvents(events) == (uint)events.Count
            ? StrokeResult.Sent(events.Count)
            : StrokeResult.Failure;
    }
}
