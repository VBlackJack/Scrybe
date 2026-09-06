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

/// <summary>
/// Low-level adapter that performs a single clipboard read or write. The platform implementation handles
/// thread affinity; the retry policy lives in <see cref="IClipboardService"/> so it can be tested
/// without a real clipboard.
/// </summary>
public interface IClipboardWriter
{
    /// <summary>Reads Unicode text from the system clipboard. May throw if the clipboard is busy.</summary>
    /// <returns>The clipboard text, or <see langword="null"/> when the clipboard contains no text.</returns>
    string? GetText();

    /// <summary>Reads text and a stable version. Unsupported writers cannot authorize clearing.</summary>
    ClipboardSnapshot? GetSnapshot() => null;

    /// <summary>Atomically clears only the specified nonzero clipboard version.</summary>
    /// <param name="version">Version captured before injection.</param>
    bool TryClear(uint version) => false;

    /// <summary>Writes <paramref name="text"/> to the system clipboard. May throw if the clipboard is busy.</summary>
    /// <param name="text">The Unicode text to place on the clipboard.</param>
    void SetText(string text);
}
