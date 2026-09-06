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
using Scrybe.Core.Snippets;

namespace Scrybe.App.Services;

public sealed partial class SnippetLibrary
{
    /// <summary>Exports one committed library snapshot under the mutation gate.</summary>
    public async Task<byte[]> ExportAsync()
    {
        await _operationGate.WaitAsync().ConfigureAwait(false);
        try { return SnippetExchange.Export(_snippets); }
        finally { _operationGate.Release(); }
    }

    /// <summary>Publishes an import once, only if the reviewed library snapshot is still current.</summary>
    public async Task<bool> ImportAsync(byte[] expectedSnapshot, IReadOnlyList<Snippet> incoming, SnippetConflictPolicy policy)
    {
        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!SnippetExchange.Export(_snippets).AsSpan().SequenceEqual(expectedSnapshot))
            { return false; }
            IReadOnlyList<Snippet> pending = SnippetExchange.Merge(_snippets, incoming, policy);
            bool saved = await _store.SaveAsync(pending).ConfigureAwait(false);
            if (saved) { _snippets.Clear(); _snippets.AddRange(pending); }
            return saved;
        }
        finally { _operationGate.Release(); }
    }
}
