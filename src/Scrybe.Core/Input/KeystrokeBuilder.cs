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
/// Pure builder converting text into an ordered sequence of keystroke intents (no Win32, no layout
/// assumptions). Printable characters (U+0020 and above) become character keystrokes; <c>\n</c>
/// becomes Enter and <c>\t</c> becomes Tab. Carriage returns are ignored; other control characters
/// are skipped and counted. The actual character→Unicode/scancode mapping is done by the injector.
/// </summary>
public static class KeystrokeBuilder
{
    private const char CarriageReturn = '\r';
    private const char LineFeed = '\n';
    private const char TabCharacter = '\t';
    private const char FirstPrintableCharacter = (char)0x20;

    /// <summary>Builds the keystroke sequence for <paramref name="text"/>.</summary>
    /// <param name="text">The text to convert.</param>
    public static KeystrokeSequence Build(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Build(text.AsSpan());
    }

    /// <summary>Builds the keystroke sequence for <paramref name="text"/> without requiring a managed string.</summary>
    /// <param name="text">The text span to convert.</param>
    public static KeystrokeSequence Build(ReadOnlySpan<char> text)
    {
        List<KeyStroke> strokes = [];
        int skipped = 0;

        foreach (char character in text)
        {
            if (TryBuildStroke(character, out KeyStroke? stroke, out bool skippedCharacter))
            {
                strokes.Add(stroke!);
            }
            else if (skippedCharacter)
            {
                skipped++;
            }
        }

        return new KeystrokeSequence(strokes, skipped);
    }

    /// <summary>Counts the logical keystrokes that would be emitted for <paramref name="text"/>.</summary>
    /// <param name="text">The text span to inspect.</param>
    public static int CountStrokes(ReadOnlySpan<char> text)
    {
        int count = 0;
        foreach (char character in text)
        {
            if (TryBuildStroke(character, out _, out _))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Counts control characters that would be skipped for <paramref name="text"/>.</summary>
    /// <param name="text">The text span to inspect.</param>
    public static int CountSkipped(ReadOnlySpan<char> text)
    {
        int count = 0;
        foreach (char character in text)
        {
            if (!TryBuildStroke(character, out _, out bool skippedCharacter) && skippedCharacter)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Maps a single input character to one logical keystroke, or reports whether it was skipped.</summary>
    /// <param name="character">The input character.</param>
    /// <param name="stroke">The resulting stroke when one should be emitted.</param>
    /// <param name="skipped">Whether the character is an unsupported control and should be counted as skipped.</param>
    public static bool TryBuildStroke(char character, out KeyStroke? stroke, out bool skipped)
    {
        skipped = false;
        stroke = character switch
        {
            CarriageReturn => null,
            LineFeed => KeyStroke.FromSpecial(SpecialKey.Enter),
            TabCharacter => KeyStroke.FromSpecial(SpecialKey.Tab),
            >= FirstPrintableCharacter => KeyStroke.FromCharacter(character),
            _ => null,
        };

        if (stroke is not null)
        {
            return true;
        }

        skipped = character != CarriageReturn;
        return false;
    }
}
