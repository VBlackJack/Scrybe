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

/// <summary>Tests for the pure, headless-testable <see cref="HotkeyParser"/>.</summary>
public sealed class HotkeyParserTests
{
    [Fact]
    public void TryParse_ValidCombination_ReturnsDefinition()
    {
        bool result = HotkeyParser.TryParse("Control+Alt", "S", out HotkeyDefinition? definition);

        result.Should().BeTrue();
        definition.Should().NotBeNull();
        definition!.Modifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Alt);
        definition.VirtualKey.Should().Be(0x53u);
        definition.DisplayName.Should().Be("Control+Alt+S");
    }

    [Theory]
    [InlineData("ctrl+alt", "s")]
    [InlineData("CONTROL ALT", "S")]
    [InlineData("alt,ctrl", "s")]
    public void TryParse_IsCaseAndSeparatorInsensitive(string modifiers, string key)
    {
        bool result = HotkeyParser.TryParse(modifiers, key, out HotkeyDefinition? definition);

        result.Should().BeTrue();
        definition!.Modifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Alt);
        definition.VirtualKey.Should().Be(0x53u);
    }

    [Fact]
    public void TryParse_FunctionKey_MapsToVirtualKey()
    {
        bool result = HotkeyParser.TryParse("Shift", "F5", out HotkeyDefinition? definition);

        result.Should().BeTrue();
        definition!.Modifiers.Should().Be(HotkeyModifiers.Shift);
        definition.VirtualKey.Should().Be(0x74u);
        definition.DisplayName.Should().Be("Shift+F5");
    }

    [Fact]
    public void TryParse_NoModifiers_IsAllowed()
    {
        bool result = HotkeyParser.TryParse(null, "A", out HotkeyDefinition? definition);

        result.Should().BeTrue();
        definition!.Modifiers.Should().Be(HotkeyModifiers.None);
        definition.VirtualKey.Should().Be(0x41u);
    }

    [Theory]
    [InlineData("Control+Alt", null)]
    [InlineData("Control+Alt", "")]
    [InlineData("Control+Alt", "Foo")]
    [InlineData("Control+Alt", "F25")]
    [InlineData("Hyper", "S")]
    public void TryParse_InvalidInput_ReturnsFalse(string? modifiers, string? key)
    {
        bool result = HotkeyParser.TryParse(modifiers, key, out HotkeyDefinition? definition);

        result.Should().BeFalse();
        definition.Should().BeNull();
    }
}
