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
using Scrybe.Core.Models;

namespace Scrybe.App.Services;

/// <summary>In-memory snippet library that persists every change through the snippet store.</summary>
public sealed class SnippetLibrary
{
    private readonly ISnippetStore _store;
    private readonly List<Snippet> _snippets = [];

    /// <summary>Initializes the library backed by the given store.</summary>
    /// <param name="store">The persistence store.</param>
    public SnippetLibrary(ISnippetStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>The current snippets.</summary>
    public IReadOnlyList<Snippet> Snippets => _snippets;

    /// <summary>Loads the snippets from the store.</summary>
    public async Task LoadAsync()
    {
        IReadOnlyList<Snippet> loaded = await _store.LoadAsync().ConfigureAwait(false);
        _snippets.Clear();
        _snippets.AddRange(loaded);
    }

    /// <summary>Adds or replaces a snippet (matched by id) and persists.</summary>
    /// <param name="snippet">The snippet to save.</param>
    /// <returns><see langword="true"/> when the snippet library was persisted; otherwise <see langword="false"/>.</returns>
    public async Task<bool> SaveAsync(Snippet snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);

        int index = _snippets.FindIndex(existing => string.Equals(existing.Id, snippet.Id, StringComparison.Ordinal));
        if (index >= 0)
        {
            _snippets[index] = snippet;
        }
        else
        {
            _snippets.Add(snippet);
        }

        return await _store.SaveAsync(_snippets).ConfigureAwait(false);
    }

    /// <summary>Deletes the snippet with the given id and persists.</summary>
    /// <param name="id">The id of the snippet to delete.</param>
    /// <returns><see langword="true"/> when the deletion was persisted; otherwise <see langword="false"/>.</returns>
    public async Task<bool> DeleteAsync(string id)
    {
        _snippets.RemoveAll(snippet => string.Equals(snippet.Id, id, StringComparison.Ordinal));
        return await _store.SaveAsync(_snippets).ConfigureAwait(false);
    }
}
