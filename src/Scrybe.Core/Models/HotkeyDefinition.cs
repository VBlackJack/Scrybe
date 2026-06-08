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
/// An immutable, fully-resolved global hotkey: the modifier flags and the Win32 virtual-key
/// code to register, plus a human-readable display name for logging.
/// </summary>
/// <param name="Modifiers">The modifier flags (already in Win32 <c>fsModifiers</c> form).</param>
/// <param name="VirtualKey">The Win32 virtual-key code of the main key.</param>
/// <param name="DisplayName">A readable representation such as <c>Control+Alt+S</c>.</param>
public sealed record HotkeyDefinition(HotkeyModifiers Modifiers, uint VirtualKey, string DisplayName);
