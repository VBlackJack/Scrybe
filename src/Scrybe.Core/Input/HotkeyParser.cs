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

using System.Globalization;
using Scrybe.Core.Models;

namespace Scrybe.Core.Input;

/// <summary>
/// Pure, side-effect-free parser that converts configuration strings (modifiers and a key)
/// into a <see cref="HotkeyDefinition"/> with Win32-compatible modifier flags and virtual-key
/// code. Parsing is case-insensitive. This is the headless-testable core of the hotkey feature;
/// actual OS registration lives in the application layer.
/// </summary>
public static class HotkeyParser
{
    private const char ModifierSeparatorPlus = '+';
    private const char ModifierSeparatorComma = ',';
    private const char ModifierSeparatorSpace = ' ';

    private const uint VirtualKeyDigitBase = 0x30;
    private const uint VirtualKeyLetterBase = 0x41;
    private const uint VirtualKeyF1 = 0x70;
    private const int HighestFunctionKey = 24;

    /// <summary>
    /// Attempts to parse the supplied modifier and key strings into a <see cref="HotkeyDefinition"/>.
    /// </summary>
    /// <param name="modifiers">
    /// Modifier tokens separated by <c>+</c>, <c>,</c> or spaces (for example <c>Ctrl+Alt</c>).
    /// May be empty or <see langword="null"/> for a hotkey with no modifiers.
    /// </param>
    /// <param name="key">A single key token: <c>A</c>–<c>Z</c>, <c>0</c>–<c>9</c>, or <c>F1</c>–<c>F24</c>.</param>
    /// <param name="definition">The parsed definition when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if both inputs were valid; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? modifiers, string? key, out HotkeyDefinition? definition)
    {
        definition = null;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!TryParseModifiers(modifiers, out HotkeyModifiers parsedModifiers))
        {
            return false;
        }

        if (!TryParseKey(key, out uint virtualKey, out string normalizedKey))
        {
            return false;
        }

        definition = new HotkeyDefinition(parsedModifiers, virtualKey, BuildDisplayName(parsedModifiers, normalizedKey));
        return true;
    }

    private static bool TryParseModifiers(string? modifiers, out HotkeyModifiers result)
    {
        result = HotkeyModifiers.None;

        if (string.IsNullOrWhiteSpace(modifiers))
        {
            return true;
        }

        string[] tokens = modifiers.Split(
            new[] { ModifierSeparatorPlus, ModifierSeparatorComma, ModifierSeparatorSpace },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string token in tokens)
        {
            switch (token.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    result |= HotkeyModifiers.Control;
                    break;
                case "ALT":
                    result |= HotkeyModifiers.Alt;
                    break;
                case "SHIFT":
                    result |= HotkeyModifiers.Shift;
                    break;
                case "WIN":
                case "WINDOWS":
                case "CMD":
                    result |= HotkeyModifiers.Windows;
                    break;
                default:
                    result = HotkeyModifiers.None;
                    return false;
            }
        }

        return true;
    }

    private static bool TryParseKey(string key, out uint virtualKey, out string normalizedKey)
    {
        virtualKey = 0;
        normalizedKey = string.Empty;

        string trimmed = key.Trim().ToUpperInvariant();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed.Length == 1)
        {
            char character = trimmed[0];

            if (character is >= 'A' and <= 'Z')
            {
                virtualKey = VirtualKeyLetterBase + (uint)(character - 'A');
                normalizedKey = trimmed;
                return true;
            }

            if (character is >= '0' and <= '9')
            {
                virtualKey = VirtualKeyDigitBase + (uint)(character - '0');
                normalizedKey = trimmed;
                return true;
            }

            return false;
        }

        if (trimmed[0] == 'F'
            && int.TryParse(trimmed.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int functionNumber)
            && functionNumber is >= 1 and <= HighestFunctionKey)
        {
            virtualKey = VirtualKeyF1 + (uint)(functionNumber - 1);
            normalizedKey = "F" + functionNumber.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static string BuildDisplayName(HotkeyModifiers modifiers, string normalizedKey)
    {
        List<string> parts = new();

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Control");
        }

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Windows");
        }

        parts.Add(normalizedKey);
        return string.Join("+", parts);
    }
}
