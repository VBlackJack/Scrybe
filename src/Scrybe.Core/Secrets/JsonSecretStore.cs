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

namespace Scrybe.Core.Secrets;

/// <summary>JSON collection store with quarantine, failed-read admission and stale-write protection.</summary>
public sealed class JsonSecretStore : ISecretStore, IStoreReadState
{
    private readonly JsonStoreFile<List<SecretEntry>> _file;

    /// <summary>Initializes the store at the supplied path.</summary>
    /// <param name="filePath">Path to the JSON store.</param>
    public JsonSecretStore(string filePath) => _file = new(filePath);

    /// <inheritdoc />
    public bool CanSave => _file.CanSave;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecretEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        (List<SecretEntry>? entries, _) = await _file.LoadAsync(
            entries => entries.All(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.Id) && !string.IsNullOrWhiteSpace(entry.Name) && !string.IsNullOrWhiteSpace(entry.ProtectedSecret)), cancellationToken).ConfigureAwait(false);
        return entries ?? [];
    }

    /// <inheritdoc />
    public Task<bool> SaveAsync(IReadOnlyList<SecretEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return _file.SaveAsync(entries.ToList(), cancellationToken);
    }
}
