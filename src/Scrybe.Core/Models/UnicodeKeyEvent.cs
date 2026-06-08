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
/// A single key event for Unicode-mode injection: either a UTF-16 code unit (for characters,
/// including surrogate halves) or a virtual-key code (for control keys such as Enter and Tab).
/// </summary>
/// <param name="Value">The UTF-16 code unit or the virtual-key code.</param>
/// <param name="IsVirtualKey"><see langword="true"/> when <paramref name="Value"/> is a virtual-key code.</param>
/// <param name="IsKeyUp"><see langword="true"/> for key release; otherwise key press.</param>
public sealed record UnicodeKeyEvent(ushort Value, bool IsVirtualKey, bool IsKeyUp);
