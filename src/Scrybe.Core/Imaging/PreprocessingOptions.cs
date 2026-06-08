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

namespace Scrybe.Core.Imaging;

/// <summary>Tunable parameters for the OCR pre-processing pipeline.</summary>
/// <param name="UpscaleFactor">Integer factor applied when text is judged too small.</param>
/// <param name="MinLineHeightPx">Estimated line height at or below which upscaling is applied.</param>
/// <param name="InkLuminanceThreshold">Luminance at or below which a pixel counts as ink.</param>
/// <param name="RowInkMinFraction">Minimum ink fraction for a row to count as a text row.</param>
/// <param name="InversionLuminanceThreshold">Border luminance below which the image is inverted.</param>
/// <param name="ApplyBinarization">Whether to hard-binarize (single-pass Otsu) before OCR.</param>
public sealed record PreprocessingOptions(
    int UpscaleFactor,
    int MinLineHeightPx,
    int InkLuminanceThreshold,
    double RowInkMinFraction,
    double InversionLuminanceThreshold,
    bool ApplyBinarization)
{
    /// <summary>The default options sourced from <see cref="AppConstants"/>.</summary>
    public static PreprocessingOptions Default { get; } = new(
        AppConstants.PreprocessUpscaleFactor,
        AppConstants.PreprocessMinLineHeightPx,
        AppConstants.PreprocessInkLuminanceThreshold,
        AppConstants.PreprocessRowInkMinFraction,
        AppConstants.PreprocessInversionLuminanceThreshold,
        AppConstants.PreprocessApplyBinarization);
}
