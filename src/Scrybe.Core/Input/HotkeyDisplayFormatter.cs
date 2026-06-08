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

namespace Scrybe.Core.Input;

/// <summary>Formats persisted hotkey settings into a stable, compact display label.</summary>
public static class HotkeyDisplayFormatter
{
    private const char ModifierSeparatorPlus = '+';
    private const char ModifierSeparatorComma = ',';
    private const char ModifierSeparatorSpace = ' ';

    /// <summary>
    /// Builds a canonical label from a modifier string and key token, ordering known modifiers as
    /// Ctrl, Alt, Shift, then Win. Empty modifiers produce a key-only label.
    /// </summary>
    /// <param name="modifiers">Modifier tokens separated by <c>+</c>, comma or spaces.</param>
    /// <param name="key">The key token.</param>
    /// <returns>A display label such as <c>Ctrl+Alt+S</c>.</returns>
    public static string Format(string modifiers, string key)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        ArgumentNullException.ThrowIfNull(key);

        string normalizedKey = key.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(modifiers))
        {
            return normalizedKey;
        }

        bool hasControl = false;
        bool hasAlt = false;
        bool hasShift = false;
        bool hasWindows = false;
        List<string> unknownTokens = [];

        string[] tokens = modifiers.Split(
            new[] { ModifierSeparatorPlus, ModifierSeparatorComma, ModifierSeparatorSpace },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string token in tokens)
        {
            switch (token.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    hasControl = true;
                    break;
                case "ALT":
                    hasAlt = true;
                    break;
                case "SHIFT":
                    hasShift = true;
                    break;
                case "WIN":
                case "WINDOWS":
                case "CMD":
                    hasWindows = true;
                    break;
                default:
                    unknownTokens.Add(token.Trim());
                    break;
            }
        }

        List<string> parts = [];
        AddKnownModifier(parts, hasControl, "Ctrl");
        AddKnownModifier(parts, hasAlt, "Alt");
        AddKnownModifier(parts, hasShift, "Shift");
        AddKnownModifier(parts, hasWindows, "Win");
        parts.AddRange(unknownTokens);

        if (!string.IsNullOrEmpty(normalizedKey))
        {
            parts.Add(normalizedKey);
        }

        return string.Join("+", parts);
    }

    private static void AddKnownModifier(List<string> parts, bool enabled, string label)
    {
        if (enabled)
        {
            parts.Add(label);
        }
    }
}
