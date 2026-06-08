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
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for hotkey binding validation and the (modifiers, key) → definition conversion.</summary>
public sealed class HotkeyBindingTests
{
    private static IReadOnlyList<HotkeyBinding> ValidSet { get; } =
    [
        new HotkeyBinding("capture", "Control+Alt", "S"),
        new HotkeyBinding("inject", "Control+Alt", "V"),
        new HotkeyBinding("abort", "Control+Alt", "Q"),
        new HotkeyBinding("palette", "Control+Alt", "P"),
    ];

    [Fact]
    public void Validate_DistinctValidSet_IsAccepted()
    {
        HotkeyBindingValidationResult result = HotkeyBindingValidator.Validate(ValidSet);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DuplicateCombo_IsRejected()
    {
        IReadOnlyList<HotkeyBinding> bindings =
        [
            new HotkeyBinding("capture", "Control+Alt", "S"),
            new HotkeyBinding("inject", "Alt+Control", "S"),
        ];

        HotkeyBindingValidationResult result = HotkeyBindingValidator.Validate(bindings);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ActionId == "inject" && e.Error == HotkeyBindingError.Duplicate);
    }

    [Fact]
    public void Validate_NoModifier_IsRejected()
    {
        IReadOnlyList<HotkeyBinding> bindings = [new HotkeyBinding("capture", string.Empty, "S")];

        HotkeyBindingValidationResult result = HotkeyBindingValidator.Validate(bindings);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ActionId == "capture" && e.Error == HotkeyBindingError.MissingModifier);
    }

    [Fact]
    public void Validate_UnparseableKey_IsRejected()
    {
        IReadOnlyList<HotkeyBinding> bindings = [new HotkeyBinding("capture", "Control+Alt", "Foo")];

        HotkeyBindingValidationResult result = HotkeyBindingValidator.Validate(bindings);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ActionId == "capture" && e.Error == HotkeyBindingError.Unparseable);
    }

    [Fact]
    public void Conversion_MultiModifierShiftedKey_IsCorrect()
    {
        bool parsed = HotkeyParser.TryParse("Control+Alt+Shift", "S", out HotkeyDefinition? definition);

        parsed.Should().BeTrue();
        definition!.Modifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift);
        definition.VirtualKey.Should().Be(0x53);
        definition.DisplayName.Should().Be("Control+Alt+Shift+S");
    }

    [Fact]
    public void Conversion_FunctionKey_IsCorrect()
    {
        bool parsed = HotkeyParser.TryParse("Control", "F2", out HotkeyDefinition? definition);

        parsed.Should().BeTrue();
        definition!.VirtualKey.Should().Be(0x71);
        definition.DisplayName.Should().Be("Control+F2");
    }

    [Fact]
    public void Registrar_RegistrationConflict_RollsBackToPreviousBindings()
    {
        FakeHotkeyService hotkeys = new("Control+Alt+X");
        HotkeyRegistrar registrar = new(hotkeys);
        registrar.Initialize(ValidSet);
        hotkeys.Registered.Should().HaveCount(4);

        List<HotkeyBinding> newSet = ValidSet
            .Select(binding => binding.ActionId == "inject" ? binding with { Key = "X" } : binding)
            .ToList();

        HotkeyRegistrarResult result = registrar.Apply(newSet);

        result.Status.Should().Be(HotkeyRegistrarStatus.RegistrationFailed);
        result.FailedActionId.Should().Be("inject");
        hotkeys.Registered["inject"].DisplayName.Should().Be("Control+Alt+V");
        registrar.Current.Should().BeEquivalentTo(ValidSet);
    }

    [Fact]
    public void Registrar_ValidSet_RegistersAndBecomesCurrent()
    {
        FakeHotkeyService hotkeys = new();
        HotkeyRegistrar registrar = new(hotkeys);

        HotkeyRegistrarResult result = registrar.Apply(ValidSet);

        result.Status.Should().Be(HotkeyRegistrarStatus.Success);
        hotkeys.Registered.Should().HaveCount(4);
        registrar.Current.Should().BeEquivalentTo(ValidSet);
    }

    private sealed class FakeHotkeyService : IHotkeyService
    {
        private readonly HashSet<string> _blockedCombos;

        public FakeHotkeyService(params string[] blockedCombos)
            => _blockedCombos = new HashSet<string>(blockedCombos, StringComparer.OrdinalIgnoreCase);

#pragma warning disable CS0067 // Required by IHotkeyService; the fake never raises it.
        public event EventHandler<string>? HotkeyPressed;
#pragma warning restore CS0067

        public Dictionary<string, HotkeyDefinition> Registered { get; } = new(StringComparer.Ordinal);

        public bool TryRegister(string id, HotkeyDefinition definition)
        {
            if (_blockedCombos.Contains(definition.DisplayName))
            {
                return false;
            }

            Registered[id] = definition;
            return true;
        }

        public void Unregister(string id) => Registered.Remove(id);

        public void Dispose()
        {
        }
    }
}
