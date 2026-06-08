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
/// Low-level seam that performs a single clipboard write. The platform implementation handles
/// thread affinity; the retry policy lives in <see cref="IClipboardService"/> so it can be tested
/// without a real clipboard.
/// </summary>
public interface IClipboardWriter
{
    /// <summary>Writes <paramref name="text"/> to the system clipboard. May throw if the clipboard is busy.</summary>
    /// <param name="text">The Unicode text to place on the clipboard.</param>
    void SetText(string text);
}
