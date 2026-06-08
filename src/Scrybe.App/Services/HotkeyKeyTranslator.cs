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
using System.Text;
using System.Windows.Input;

namespace Scrybe.App.Services;

/// <summary>
/// Translates a captured WPF <see cref="Key"/> and <see cref="ModifierKeys"/> into the token strings
/// the headless <c>HotkeyParser</c> understands, so the recorder hands raw captured input to Core.
/// </summary>
public static class HotkeyKeyTranslator
{
    /// <summary>Returns whether a key is a modifier on its own (so the recorder keeps waiting for a main key).</summary>
    /// <param name="key">The key to test.</param>
    public static bool IsModifierKey(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift
        or Key.LWin or Key.RWin
        or Key.System;

    /// <summary>Translates the active modifier flags into a token string such as <c>Control+Alt</c>.</summary>
    /// <param name="modifiers">The active modifier flags.</param>
    public static string TranslateModifiers(ModifierKeys modifiers)
    {
        StringBuilder builder = new();
        Append(builder, modifiers.HasFlag(ModifierKeys.Control), "Control");
        Append(builder, modifiers.HasFlag(ModifierKeys.Alt), "Alt");
        Append(builder, modifiers.HasFlag(ModifierKeys.Shift), "Shift");
        Append(builder, modifiers.HasFlag(ModifierKeys.Windows), "Windows");
        return builder.ToString();
    }

    /// <summary>Translates a main key into a token (<c>A</c>–<c>Z</c>, <c>0</c>–<c>9</c>, <c>F1</c>–<c>F24</c>), or null.</summary>
    /// <param name="key">The captured main key.</param>
    public static string? TranslateKey(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return ((char)('A' + (key - Key.A))).ToString();
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return ((char)('0' + (key - Key.D0))).ToString();
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return ((char)('0' + (key - Key.NumPad0))).ToString();
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            return "F" + (key - Key.F1 + 1).ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static void Append(StringBuilder builder, bool present, string token)
    {
        if (!present)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('+');
        }

        builder.Append(token);
    }
}
