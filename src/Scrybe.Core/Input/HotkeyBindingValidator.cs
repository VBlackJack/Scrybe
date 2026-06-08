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

/// <summary>Why a hotkey binding was rejected by the validator.</summary>
public enum HotkeyBindingError
{
    /// <summary>The binding is valid.</summary>
    None = 0,

    /// <summary>The modifiers/key could not be parsed into a definition.</summary>
    Unparseable,

    /// <summary>A global hotkey requires at least one modifier; this binding has none.</summary>
    MissingModifier,

    /// <summary>The combo duplicates another action's combo.</summary>
    Duplicate,
}

/// <summary>A single invalid binding and the reason it was rejected.</summary>
/// <param name="ActionId">The offending action identifier.</param>
/// <param name="Error">The reason for rejection.</param>
public sealed record HotkeyBindingValidationEntry(string ActionId, HotkeyBindingError Error);

/// <summary>The outcome of validating the full hotkey binding set.</summary>
/// <param name="IsValid">Whether every binding is valid and distinct.</param>
/// <param name="Errors">The rejected bindings (empty when valid).</param>
public sealed record HotkeyBindingValidationResult(bool IsValid, IReadOnlyList<HotkeyBindingValidationEntry> Errors);

/// <summary>
/// Pure validator over the full set of hotkey bindings: each must be parseable, carry at least one
/// modifier, and be distinct from the others. The result names every offending action for localized
/// messaging, so a bad edit can be rejected before any OS registration is attempted.
/// </summary>
public static class HotkeyBindingValidator
{
    /// <summary>Validates the binding set.</summary>
    /// <param name="bindings">The bindings for every action.</param>
    public static HotkeyBindingValidationResult Validate(IReadOnlyList<HotkeyBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        List<HotkeyBindingValidationEntry> errors = [];
        Dictionary<string, string> combosSeen = new(StringComparer.OrdinalIgnoreCase);

        foreach (HotkeyBinding binding in bindings)
        {
            if (!HotkeyParser.TryParse(binding.Modifiers, binding.Key, out HotkeyDefinition? definition) || definition is null)
            {
                errors.Add(new HotkeyBindingValidationEntry(binding.ActionId, HotkeyBindingError.Unparseable));
                continue;
            }

            if (definition.Modifiers == HotkeyModifiers.None)
            {
                errors.Add(new HotkeyBindingValidationEntry(binding.ActionId, HotkeyBindingError.MissingModifier));
                continue;
            }

            if (combosSeen.ContainsKey(definition.DisplayName))
            {
                errors.Add(new HotkeyBindingValidationEntry(binding.ActionId, HotkeyBindingError.Duplicate));
                continue;
            }

            combosSeen[definition.DisplayName] = binding.ActionId;
        }

        return new HotkeyBindingValidationResult(errors.Count == 0, errors);
    }
}
