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

namespace Scrybe.Core.Text;

/// <summary>Toggles for the individual stages of the text post-processor, composed per cleanup mode.</summary>
/// <param name="RepairWraps">Merge lines broken by a narrow terminal back into the previous line.</param>
/// <param name="StripPrompts">Remove leading shell prompts from line starts.</param>
/// <param name="FixConfusions">Apply context-aware OCR confusion corrections inside technical tokens.</param>
/// <param name="StripLogDecorations">Strip leading timestamps, log levels and PIDs (Log Cleaner).</param>
public sealed record TextPostProcessingOptions(
    bool RepairWraps,
    bool StripPrompts,
    bool FixConfusions,
    bool StripLogDecorations)
{
    /// <summary>Raw: a true passthrough with no transforms (the safety net).</summary>
    public static TextPostProcessingOptions Raw { get; } = new(false, false, false, false);

    /// <summary>Standard: wrap repair, prompt stripping and confusion correction (the default profile).</summary>
    public static TextPostProcessingOptions Standard { get; } = new(true, true, true, false);

    /// <summary>Log Cleaner: Standard plus conservative log-decoration stripping.</summary>
    public static TextPostProcessingOptions LogCleaner { get; } = new(true, true, true, true);

    /// <summary>Code Formatter: only validated confusion fixes; never merges lines or strips prompts.</summary>
    public static TextPostProcessingOptions CodeFormatter { get; } = new(false, false, true, false);

    /// <summary>The Standard profile, kept as the default used where no mode is specified.</summary>
    public static TextPostProcessingOptions Default => Standard;

    /// <summary>Returns the option set for a cleanup mode.</summary>
    /// <param name="mode">The cleanup mode.</param>
    public static TextPostProcessingOptions ForMode(OcrCleanupMode mode) => mode switch
    {
        OcrCleanupMode.Raw => Raw,
        OcrCleanupMode.Standard => Standard,
        OcrCleanupMode.LogCleaner => LogCleaner,
        OcrCleanupMode.CodeFormatter => CodeFormatter,
        _ => Standard,
    };
}
