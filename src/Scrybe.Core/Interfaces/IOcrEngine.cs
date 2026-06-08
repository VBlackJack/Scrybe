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
/// Recognizes text from an encoded image. Implementations keep the underlying engine warm and
/// serialize access, since native OCR engines are typically not thread-safe.
/// </summary>
public interface IOcrEngine
{
    /// <summary>Recognizes text from an encoded image (for example PNG bytes).</summary>
    /// <param name="imageBytes">The encoded image to recognize.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The recognized text and mean confidence.</returns>
    Task<OcrResult> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken = default);

    /// <summary>Eagerly initializes the engine so the first recognition is fast. Safe to call repeatedly.</summary>
    void Prewarm();
}
