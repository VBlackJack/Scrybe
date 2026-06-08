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

namespace Scrybe.Core.Models;

/// <summary>The outcome of an OCR pass: the recognized text and the engine's mean confidence.</summary>
/// <param name="Text">The recognized text.</param>
/// <param name="MeanConfidence">Mean confidence in the range 0–100, or 0 when unavailable.</param>
public sealed record OcrResult(string Text, float MeanConfidence);
