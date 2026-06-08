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

/// <summary>A configurable global hotkey binding for one action, in raw (parseable) string form.</summary>
/// <param name="ActionId">The action identifier (for example the capture hotkey id).</param>
/// <param name="Modifiers">The modifier tokens (for example <c>Control+Alt</c>).</param>
/// <param name="Key">The key token (for example <c>S</c> or <c>F2</c>).</param>
public sealed record HotkeyBinding(string ActionId, string Modifiers, string Key);
