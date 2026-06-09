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

/// <summary>Persists user settings. Implementations must never crash on a missing or corrupt store.</summary>
public interface ISettingsStore
{
    /// <summary>
    /// Loads and validates the settings, returning validated defaults when the store is missing or
    /// unreadable. Whether the file existed is reported so the caller can write defaults on first run.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the load.</param>
    Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the settings, replacing the previous contents.</summary>
    /// <param name="settings">The settings to save.</param>
    /// <param name="cancellationToken">Token used to cancel the save.</param>
    /// <returns><see langword="true"/> when the settings were written; otherwise <see langword="false"/>.</returns>
    Task<bool> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>The outcome of loading settings.</summary>
/// <param name="Settings">The validated settings (defaults when none existed or the file was corrupt).</param>
/// <param name="Existed">Whether a settings file was present and readable.</param>
public sealed record SettingsLoadResult(AppSettings Settings, bool Existed);
