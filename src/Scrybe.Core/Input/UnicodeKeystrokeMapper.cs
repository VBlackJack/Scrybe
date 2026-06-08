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

/// <summary>
/// Pure mapper from a <see cref="KeyStroke"/> to Unicode-mode key events. Characters become their
/// UTF-16 code unit (so surrogate halves are sent individually and recombined by the OS); Enter and
/// Tab become virtual-key events since they have no codepoint.
/// </summary>
public static class UnicodeKeystrokeMapper
{
    /// <summary>Maps a keystroke to its ordered Unicode-mode key events (a down then an up).</summary>
    /// <param name="stroke">The keystroke to map.</param>
    public static IReadOnlyList<UnicodeKeyEvent> Map(KeyStroke stroke)
    {
        ArgumentNullException.ThrowIfNull(stroke);

        if (stroke.IsSpecial)
        {
            ushort virtualKey = stroke.Special == SpecialKey.Enter ? AppConstants.VkReturn : AppConstants.VkTab;
            return
            [
                new UnicodeKeyEvent(virtualKey, IsVirtualKey: true, IsKeyUp: false),
                new UnicodeKeyEvent(virtualKey, IsVirtualKey: true, IsKeyUp: true),
            ];
        }

        ushort codeUnit = stroke.Character;
        return
        [
            new UnicodeKeyEvent(codeUnit, IsVirtualKey: false, IsKeyUp: false),
            new UnicodeKeyEvent(codeUnit, IsVirtualKey: false, IsKeyUp: true),
        ];
    }
}
