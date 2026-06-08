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

using FluentAssertions;
using Scrybe.Core.Input;
using Scrybe.Core.Models;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>
/// Tests for the pure <see cref="KeystrokeBuilder"/>: it emits layout-independent keystroke intents
/// (characters and special keys) and reports skipped characters. The character→scancode/shift mapping
/// is the injector's responsibility (against the active keyboard layout), so it is not asserted here.
/// </summary>
public sealed class KeystrokeBuilderTests
{
    [Fact]
    public void Build_PrintableCharacters_EmitsCharacterStrokes()
    {
        KeystrokeSequence sequence = KeystrokeBuilder.Build("Hi!");

        sequence.Strokes.Should().Equal(
            KeyStroke.FromCharacter('H'),
            KeyStroke.FromCharacter('i'),
            KeyStroke.FromCharacter('!'));
        sequence.SkippedCharacters.Should().Be(0);
    }

    [Fact]
    public void Build_ReadOnlySpan_YieldsSameStrokesAsStringPath()
    {
        char[] text = "SafeSecret#2026!\n".ToCharArray();

        KeystrokeSequence fromString = KeystrokeBuilder.Build(new string(text));
        KeystrokeSequence fromSpan = KeystrokeBuilder.Build(text.AsSpan());

        fromSpan.Should().BeEquivalentTo(fromString);
        KeystrokeBuilder.CountStrokes(text.AsSpan()).Should().Be(fromString.Strokes.Count);
        KeystrokeBuilder.CountSkipped(text.AsSpan()).Should().Be(fromString.SkippedCharacters);
    }

    [Fact]
    public void Build_Newline_EmitsEnter()
    {
        KeystrokeSequence sequence = KeystrokeBuilder.Build("\n");

        sequence.Strokes.Should().Equal(KeyStroke.FromSpecial(SpecialKey.Enter));
    }

    [Fact]
    public void Build_Tab_EmitsTab()
    {
        KeystrokeSequence sequence = KeystrokeBuilder.Build("\t");

        sequence.Strokes.Should().Equal(KeyStroke.FromSpecial(SpecialKey.Tab));
    }

    [Fact]
    public void Build_MixedSpecialsAndCharacters_PreservesOrder()
    {
        KeystrokeSequence sequence = KeystrokeBuilder.Build("a\tb\nc");

        sequence.Strokes.Should().Equal(
            KeyStroke.FromCharacter('a'),
            KeyStroke.FromSpecial(SpecialKey.Tab),
            KeyStroke.FromCharacter('b'),
            KeyStroke.FromSpecial(SpecialKey.Enter),
            KeyStroke.FromCharacter('c'));
    }

    [Fact]
    public void Build_NonSafeAsciiCharacter_IsSkippedAndCounted()
    {
        KeystrokeSequence sequence = KeystrokeBuilder.Build("aéb");

        sequence.SkippedCharacters.Should().Be(1);
        sequence.Strokes.Should().Equal(
            KeyStroke.FromCharacter('a'),
            KeyStroke.FromCharacter('b'));
    }

    [Fact]
    public void Build_CarriageReturn_IsIgnored()
    {
        KeystrokeSequence sequence = KeystrokeBuilder.Build("\r");

        sequence.Strokes.Should().BeEmpty();
        sequence.SkippedCharacters.Should().Be(0);
    }
}
