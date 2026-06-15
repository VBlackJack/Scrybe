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
/// <param name="Preview">Single-line metadata preview that does not expose protected plaintext.</param>
/// <param name="Timestamp">Localized capture timestamp.</param>
/// <param name="CharCount">Plaintext character count.</param>
public sealed record HistoryPaletteListItem(string Id, string Preview, string Timestamp, int CharCount)
{
    /// <summary>Builds a display item from metadata only, without revealing protected text.</summary>
    /// <param name="entry">The protected history entry.</param>
    /// <param name="protectedPreview">The localized placeholder shown until the user reveals or copies text.</param>
    public static HistoryPaletteListItem FromMetadata(CaptureHistoryEntry entry, string protectedPreview)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(protectedPreview);

        string timestamp = entry.CapturedAtUtc
            .ToLocalTime()
            .ToString(AppConstants.CaptureHistoryTimestampFormat, CultureInfo.CurrentCulture);

        return new HistoryPaletteListItem(entry.Id, protectedPreview, timestamp, entry.CharCount);
    }
}
