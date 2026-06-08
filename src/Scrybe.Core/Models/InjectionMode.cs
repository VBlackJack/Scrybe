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

/// <summary>The keystroke injection strategy.</summary>
public enum InjectionMode
{
    /// <summary>Inject characters as Unicode codepoints (layout-independent; no scancode, Shift or AltGr).</summary>
    Unicode = 0,

    /// <summary>Inject characters as hardware scancodes resolved against the active keyboard layout.</summary>
    Scancode,
}
