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

/// <summary>Holds the most recently recognized text so the injection flow can type it back.</summary>
public interface IOcrTextStore
{
    /// <summary>The most recently stored OCR text, or <see langword="null"/> if none yet.</summary>
    string? LastText { get; }

    /// <summary>Stores <paramref name="text"/> as the most recent OCR result.</summary>
    /// <param name="text">The recognized text to retain.</param>
    void Set(string text);
}
