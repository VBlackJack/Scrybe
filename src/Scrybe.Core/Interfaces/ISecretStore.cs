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

/// <summary>Persistence boundary for DPAPI-protected secrets.</summary>
public interface ISecretStore
{
    /// <summary>Loads the persisted secrets, returning an empty library when none exist.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The protected secret entries.</returns>
    Task<IReadOnlyList<SecretEntry>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the full protected secret list.</summary>
    /// <param name="secrets">The protected secret entries to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the vault was written; otherwise <see langword="false"/>.</returns>
    Task<bool> SaveAsync(IReadOnlyList<SecretEntry> secrets, CancellationToken cancellationToken = default);
}
