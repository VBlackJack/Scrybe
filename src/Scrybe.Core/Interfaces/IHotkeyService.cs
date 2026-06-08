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

namespace Scrybe.Core.Interfaces;

/// <summary>
/// Registers one or more system-wide hotkeys, each identified by a caller-chosen id, and raises an
/// event carrying that id when a hotkey is pressed. Implementations own the OS registration and must
/// release it on disposal.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>Raised on the UI thread when a registered hotkey is pressed, carrying its id.</summary>
    event EventHandler<string>? HotkeyPressed;

    /// <summary>Registers <paramref name="definition"/> under <paramref name="id"/>, replacing any prior registration for that id.</summary>
    /// <param name="id">A caller-chosen identifier returned with the press event.</param>
    /// <param name="definition">The resolved hotkey to register.</param>
    /// <returns><see langword="true"/> if registration succeeded; otherwise <see langword="false"/>.</returns>
    bool TryRegister(string id, HotkeyDefinition definition);

    /// <summary>Releases the OS registration for <paramref name="id"/>, if any.</summary>
    /// <param name="id">The identifier used at registration.</param>
    void Unregister(string id);
}
