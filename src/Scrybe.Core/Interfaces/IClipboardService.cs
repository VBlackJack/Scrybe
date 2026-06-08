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

namespace Scrybe.Core.Interfaces;

/// <summary>
/// Reads and writes system clipboard text, transparently retrying the transient "clipboard busy"
/// failures that occur when another process holds the clipboard. Never throws.
/// </summary>
public interface IClipboardService
{
    /// <summary>Reads text from the clipboard, retrying on transient failures.</summary>
    /// <param name="cancellationToken">Token used to cancel between retries.</param>
    /// <returns>The clipboard text, or <see langword="null"/> when unavailable or non-text.</returns>
    Task<string?> GetTextAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets <paramref name="text"/> on the clipboard, retrying on transient failures.</summary>
    /// <param name="text">The text to place on the clipboard.</param>
    /// <param name="cancellationToken">Token used to cancel between retries.</param>
    Task SetTextAsync(string text, CancellationToken cancellationToken = default);
}
