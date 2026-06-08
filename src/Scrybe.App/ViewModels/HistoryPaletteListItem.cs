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

using System.Globalization;
using Scrybe.Core;
using Scrybe.Core.Models;

namespace Scrybe.App.ViewModels;

/// <summary>Display item for one capture-history palette entry.</summary>
/// <param name="Id">History entry id.</param>
/// <param name="Preview">Single-line runtime plaintext preview.</param>
/// <param name="Timestamp">Localized capture timestamp.</param>
/// <param name="CharCount">Plaintext character count.</param>
public sealed record HistoryPaletteListItem(string Id, string Preview, string Timestamp, int CharCount)
{
    /// <summary>Builds a display item from a protected entry and its runtime plaintext.</summary>
    /// <param name="entry">The protected history entry.</param>
    /// <param name="text">The revealed plaintext used only for the runtime preview.</param>
    public static HistoryPaletteListItem FromEntry(CaptureHistoryEntry entry, string text)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(text);

        string timestamp = entry.CapturedAtUtc
            .ToLocalTime()
            .ToString(AppConstants.CaptureHistoryTimestampFormat, CultureInfo.CurrentCulture);

        return new HistoryPaletteListItem(entry.Id, BuildPreview(text), timestamp, entry.CharCount);
    }

    private static string BuildPreview(string text)
    {
        string singleLine = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (singleLine.Length <= AppConstants.CaptureHistoryPreviewMaxChars)
        {
            return singleLine;
        }

        int visibleLength = Math.Max(
            0,
            AppConstants.CaptureHistoryPreviewMaxChars - AppConstants.CaptureHistoryPreviewSuffix.Length);
        return singleLine[..visibleLength] + AppConstants.CaptureHistoryPreviewSuffix;
    }
}
