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

/// <summary>Persists DPAPI-protected OCR capture history entries.</summary>
public interface ICaptureHistoryStore
{
    /// <summary>Loads protected capture history entries, newest first.</summary>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    Task<IReadOnlyList<CaptureHistoryEntry>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves protected capture history entries, newest first.</summary>
    /// <param name="entries">The entries to persist.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns><see langword="true"/> when the history was written; otherwise <see langword="false"/>.</returns>
    Task<bool> SaveAsync(IReadOnlyList<CaptureHistoryEntry> entries, CancellationToken cancellationToken = default);
}
