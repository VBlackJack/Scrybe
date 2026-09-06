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

namespace Scrybe.Core.Snippets;

/// <summary>JSON collection store with quarantine, failed-read admission and stale-write protection.</summary>
public sealed class JsonSnippetStore : ISnippetStore, IStoreReadState
{
    private readonly JsonStoreFile<List<Snippet>> _file;

    /// <summary>Initializes the store at the supplied path.</summary>
    /// <param name="filePath">Path to the JSON store.</param>
    public JsonSnippetStore(string filePath) => _file = new(filePath);

    /// <inheritdoc />
    public bool CanSave => _file.CanSave;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Snippet>> LoadAsync(CancellationToken cancellationToken = default)
    {
        (List<Snippet>? entries, _) = await _file.LoadAsync(
            entries => entries.All(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.Id) && !string.IsNullOrWhiteSpace(entry.Name) && entry.Template is not null && entry.Parameters is not null && entry.Parameters.All(parameter => parameter is not null && !string.IsNullOrWhiteSpace(parameter.Name) && parameter.Label is not null)), cancellationToken).ConfigureAwait(false);
        return entries ?? [];
    }

    /// <inheritdoc />
    public Task<bool> SaveAsync(IReadOnlyList<Snippet> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return _file.SaveAsync(entries.ToList(), cancellationToken);
    }
}
