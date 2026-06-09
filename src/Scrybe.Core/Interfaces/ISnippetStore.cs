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

/// <summary>Persists the non-secret snippet library. Implementations must never crash on a missing or corrupt store.</summary>
public interface ISnippetStore
{
    /// <summary>Loads all snippets, returning an empty list when the store is missing or unreadable.</summary>
    /// <param name="cancellationToken">Token used to cancel the load.</param>
    Task<IReadOnlyList<Snippet>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the full snippet list, replacing the previous contents.</summary>
    /// <param name="snippets">The snippets to save.</param>
    /// <param name="cancellationToken">Token used to cancel the save.</param>
    /// <returns><see langword="true"/> when the library was written; otherwise <see langword="false"/>.</returns>
    Task<bool> SaveAsync(IReadOnlyList<Snippet> snippets, CancellationToken cancellationToken = default);
}
