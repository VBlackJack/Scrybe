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

using Scrybe.Core.Interfaces;
using Scrybe.Core.IO;
using Scrybe.Core.Models;

namespace Scrybe.Core.Settings;

/// <summary>Validated settings with quarantine and stale-write/failed-read protection.</summary>
public sealed class JsonSettingsStore : ISettingsStore, IStoreReadState
{
    private readonly JsonStoreFile<AppSettings> _file;

    /// <summary>Initializes the settings store.</summary>
    /// <param name="filePath">Path to settings JSON.</param>
    public JsonSettingsStore(string filePath) => _file = new(filePath);

    /// <inheritdoc />
    public bool CanSave => _file.CanSave;

    /// <inheritdoc />
    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        (AppSettings? settings, bool existed) = await _file.LoadAsync(_ => true, cancellationToken).ConfigureAwait(false);
        return new SettingsLoadResult(SettingsValidator.Validate(settings ?? new AppSettings()), existed);
    }

    /// <inheritdoc />
    public Task<bool> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        => _file.SaveAsync(settings, cancellationToken);
}
