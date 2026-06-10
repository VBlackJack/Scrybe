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

namespace Scrybe.Core.Models;

/// <summary>
/// One logical keystroke intent: either a character to type or a special key. The character is
/// resolved by the active injector at injection time, so the pure builder stays layout-independent.
/// </summary>
public sealed record KeyStroke
{
    private KeyStroke(char character, SpecialKey special)
    {
        Character = character;
        Special = special;
    }

    /// <summary>The character to type when this is not a special key.</summary>
    public char Character { get; }

    /// <summary>The special key, or <see cref="SpecialKey.None"/> when this carries a character.</summary>
    public SpecialKey Special { get; }

    /// <summary>Whether this keystroke is a special key rather than a character.</summary>
    public bool IsSpecial => Special != SpecialKey.None;

    /// <summary>Creates a character keystroke.</summary>
    /// <param name="character">The character to type.</param>
    public static KeyStroke FromCharacter(char character) => new(character, SpecialKey.None);

    /// <summary>Creates a special-key keystroke.</summary>
    /// <param name="special">The special key.</param>
    public static KeyStroke FromSpecial(SpecialKey special) => new('\0', special);
}
