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

using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;

namespace Scrybe.Core.Input;

/// <summary>The outcome of applying a new hotkey binding set.</summary>
public enum HotkeyRegistrarStatus
{
    /// <summary>Every binding was registered.</summary>
    Success = 0,

    /// <summary>The OS refused a registration (combo already held); the previous set was restored.</summary>
    RegistrationFailed,
}

/// <summary>The result of applying bindings, naming the action that failed when registration is refused.</summary>
/// <param name="Status">Whether the apply succeeded or rolled back.</param>
/// <param name="FailedActionId">The action whose registration failed, when applicable.</param>
public sealed record HotkeyRegistrarResult(HotkeyRegistrarStatus Status, string? FailedActionId);

/// <summary>
/// Owns the live registration of the user-editable global hotkeys. Re-binding is atomic: the new set is
/// registered as a whole, and if the OS refuses any combo the partial set is unwound and the previous
/// working bindings are restored, so the app is never left without working hotkeys. No WPF dependency,
/// so the rollback behavior is unit-testable against a fake hotkey service.
/// </summary>
public sealed class HotkeyRegistrar
{
    private readonly IHotkeyService _hotkeys;
    private IReadOnlyList<HotkeyBinding> _current = [];

    /// <summary>Initializes the registrar over the hotkey service.</summary>
    /// <param name="hotkeys">The OS hotkey registration service.</param>
    public HotkeyRegistrar(IHotkeyService hotkeys)
    {
        ArgumentNullException.ThrowIfNull(hotkeys);
        _hotkeys = hotkeys;
    }

    /// <summary>The currently registered bindings.</summary>
    public IReadOnlyList<HotkeyBinding> Current => _current;

    /// <summary>
    /// Best-effort initial registration at startup: each binding is registered independently so one
    /// conflicting combo never blocks the others. The successfully registered bindings become current.
    /// </summary>
    /// <param name="bindings">The bindings loaded from settings.</param>
    public void Initialize(IReadOnlyList<HotkeyBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        List<HotkeyBinding> registered = [];
        foreach (HotkeyBinding binding in bindings)
        {
            if (TryRegister(binding))
            {
                registered.Add(binding);
            }
        }

        _current = registered;
    }

    /// <summary>
    /// Atomically applies a new binding set, rolling back to the previous working set if the OS refuses
    /// any combo. Assumes the set has already passed <see cref="HotkeyBindingValidator"/>.
    /// </summary>
    /// <param name="bindings">The new bindings to register.</param>
    public HotkeyRegistrarResult Apply(IReadOnlyList<HotkeyBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        IReadOnlyList<HotkeyBinding> previous = _current;
        foreach (HotkeyBinding binding in previous)
        {
            _hotkeys.Unregister(binding.ActionId);
        }

        List<HotkeyBinding> registered = [];
        foreach (HotkeyBinding binding in bindings)
        {
            if (TryRegister(binding))
            {
                registered.Add(binding);
                continue;
            }

            FileLogger.Warn($"Hotkey registration failed for '{binding.ActionId}'; rolling back to previous bindings.");
            foreach (HotkeyBinding applied in registered)
            {
                _hotkeys.Unregister(applied.ActionId);
            }

            foreach (HotkeyBinding restored in previous)
            {
                TryRegister(restored);
            }

            _current = previous;
            return new HotkeyRegistrarResult(HotkeyRegistrarStatus.RegistrationFailed, binding.ActionId);
        }

        _current = bindings;
        return new HotkeyRegistrarResult(HotkeyRegistrarStatus.Success, null);
    }

    private bool TryRegister(HotkeyBinding binding)
    {
        if (HotkeyParser.TryParse(binding.Modifiers, binding.Key, out HotkeyDefinition? definition) && definition is not null)
        {
            return _hotkeys.TryRegister(binding.ActionId, definition);
        }

        FileLogger.Warn($"Unparseable hotkey binding for '{binding.ActionId}': '{binding.Modifiers}' + '{binding.Key}'.");
        return false;
    }
}
