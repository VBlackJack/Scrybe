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
/// Sends a built key sequence to the foreground window as synthetic keystrokes. Implementations pace
/// the keystrokes, surface UIPI elevation failures rather than failing silently, and honor cancellation
/// for the emergency abort.
/// </summary>
public interface IKeystrokeInjector
{
    /// <summary>Injects <paramref name="sequence"/> into the foreground window.</summary>
    /// <param name="sequence">The key sequence to send.</param>
    /// <param name="cancellationToken">Token used by the emergency abort to cancel injection.</param>
    /// <returns>The outcome, including whether UIPI blocked it or it was aborted.</returns>
    Task<InjectionResult> InjectAsync(KeystrokeSequence sequence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Injects plaintext characters without first materializing a full keystroke sequence. Intended for
    /// caller-owned secret buffers that are cleared after typing.
    /// </summary>
    /// <param name="text">The character buffer to type.</param>
    /// <param name="cancellationToken">Token used by the emergency abort to cancel injection.</param>
    /// <returns>The outcome, including whether UIPI blocked it or it was aborted.</returns>
    Task<InjectionResult> InjectAsync(ReadOnlyMemory<char> text, CancellationToken cancellationToken = default);
}
