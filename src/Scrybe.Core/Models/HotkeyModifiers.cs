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

namespace Scrybe.Core.Models;

/// <summary>
/// Keyboard modifier flags for a global hotkey. The numeric values intentionally match the
/// Win32 <c>RegisterHotKey</c> <c>fsModifiers</c> flags so they can be passed straight through
/// without translation.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    /// <summary>No modifier.</summary>
    None = 0x0000,

    /// <summary>The ALT key (Win32 <c>MOD_ALT</c>).</summary>
    Alt = 0x0001,

    /// <summary>The CTRL key (Win32 <c>MOD_CONTROL</c>).</summary>
    Control = 0x0002,

    /// <summary>The SHIFT key (Win32 <c>MOD_SHIFT</c>).</summary>
    Shift = 0x0004,

    /// <summary>The Windows key (Win32 <c>MOD_WIN</c>).</summary>
    Windows = 0x0008,
}
