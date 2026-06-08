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

/// <summary>Selectable OCR cleanup profile applied to the recognized text before the clipboard.</summary>
public enum OcrCleanupMode
{
    /// <summary>No transformation: verbatim OCR. The safety net.</summary>
    Raw = 0,

    /// <summary>Line-wrap repair, prompt stripping and context-aware confusion correction.</summary>
    Standard,

    /// <summary>Standard plus conservative stripping of leading timestamps, log levels and PIDs.</summary>
    LogCleaner,

    /// <summary>Indentation-preserving: no line merging, no prompt stripping, only validated confusion fixes.</summary>
    CodeFormatter,
}
