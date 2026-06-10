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

namespace Scrybe.App.Services;

/// <summary>
/// Manages the per-user "start with Windows" autostart entry. The registry is the single source of
/// truth: no setting mirrors it, so the reported state always matches what Windows will actually do
/// even after the user disables the entry from Task Manager's Startup tab.
/// </summary>
public interface IStartupRegistration
{
    /// <summary>Returns whether the autostart entry currently exists.</summary>
    bool IsEnabled();

    /// <summary>Returns the raw registered command string, or <see langword="null"/> when absent.</summary>
    string? GetRegisteredCommand();

    /// <summary>Registers the current executable to launch at logon.</summary>
    void Enable();

    /// <summary>Removes the autostart entry; a no-op when it is already absent.</summary>
    void Disable();

    /// <summary>
    /// Re-registers the current executable when an existing entry points at a now-missing executable
    /// (the install was moved or replaced). Leaves a foreign entry that still resolves untouched, and
    /// never throws, so autostart healing cannot block application startup.
    /// </summary>
    void HealIfStale();
}
