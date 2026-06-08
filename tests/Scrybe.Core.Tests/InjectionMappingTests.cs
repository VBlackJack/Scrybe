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
using Scrybe.Core;
using Scrybe.Core.Input;
using Scrybe.Core.Models;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for the pure Unicode mapper, the scancode Shift state machine, and the reference text.</summary>
public sealed class InjectionMappingTests
{
    [Fact]
    public void UnicodeMapper_Character_EmitsCodeUnitDownThenUp()
    {
        IReadOnlyList<UnicodeKeyEvent> events = UnicodeKeystrokeMapper.Map(KeyStroke.FromCharacter('A'));

        events.Should().Equal(
            new UnicodeKeyEvent(0x41, IsVirtualKey: false, IsKeyUp: false),
            new UnicodeKeyEvent(0x41, IsVirtualKey: false, IsKeyUp: true));
    }

    [Fact]
    public void UnicodeMapper_Enter_EmitsReturnVirtualKey()
    {
        IReadOnlyList<UnicodeKeyEvent> events = UnicodeKeystrokeMapper.Map(KeyStroke.FromSpecial(SpecialKey.Enter));

        events.Should().Equal(
            new UnicodeKeyEvent(AppConstants.VkReturn, IsVirtualKey: true, IsKeyUp: false),
            new UnicodeKeyEvent(AppConstants.VkReturn, IsVirtualKey: true, IsKeyUp: true));
    }

    [Fact]
    public void UnicodeMapper_Tab_EmitsTabVirtualKey()
    {
        IReadOnlyList<UnicodeKeyEvent> events = UnicodeKeystrokeMapper.Map(KeyStroke.FromSpecial(SpecialKey.Tab));

        events[0].Value.Should().Be(AppConstants.VkTab);
        events[0].IsVirtualKey.Should().BeTrue();
    }

    [Fact]
    public void UnicodeMapper_SurrogateHalf_IsSentAsItsCodeUnit()
    {
        // High surrogate of U+1D400 ('𝐀'); each UTF-16 code unit is emitted and recombined by the OS.
        const char highSurrogate = '\uD835';
        IReadOnlyList<UnicodeKeyEvent> events = UnicodeKeystrokeMapper.Map(KeyStroke.FromCharacter(highSurrogate));

        events.Should().Equal(
            new UnicodeKeyEvent(0xD835, IsVirtualKey: false, IsKeyUp: false),
            new UnicodeKeyEvent(0xD835, IsVirtualKey: false, IsKeyUp: true));
    }

    [Theory]
    [InlineData(false, true, true, false, true)]   // need shift, not held -> press, now held
    [InlineData(true, false, false, true, false)]  // don't need shift, held -> release, now not held
    [InlineData(true, true, false, false, true)]   // need shift, already held -> no change
    [InlineData(false, false, false, false, false)] // don't need shift, not held -> no change
    public void ShiftStateMachine_Transitions(
        bool currentlyHeld, bool needsShift, bool expectDown, bool expectUp, bool expectHeld)
    {
        ShiftTransition transition = ShiftStateMachine.Next(currentlyHeld, needsShift);

        transition.EmitShiftDown.Should().Be(expectDown);
        transition.EmitShiftUp.Should().Be(expectUp);
        transition.ShiftHeld.Should().Be(expectHeld);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void ReferenceText_OfLength_HasExactLengthAndIsDeterministic(int length)
    {
        string first = ReferenceText.OfLength(length);
        string second = ReferenceText.OfLength(length);

        first.Length.Should().Be(length);
        second.Should().Be(first);
    }

    [Fact]
    public void ReferenceText_OfLength_NonPositive_IsEmpty()
    {
        ReferenceText.OfLength(0).Should().BeEmpty();
    }
}
