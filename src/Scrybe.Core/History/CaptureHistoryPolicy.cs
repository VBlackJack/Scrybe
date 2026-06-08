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

namespace Scrybe.Core.History;

/// <summary>Pure capture-history ring-buffer policy.</summary>
public static class CaptureHistoryPolicy
{
    /// <summary>Prepends an entry and truncates the list to the configured maximum.</summary>
    /// <param name="existing">Existing entries, newest first.</param>
    /// <param name="entry">New entry to place first.</param>
    /// <param name="maxEntries">Maximum entries to keep; non-positive values use the default.</param>
    public static IReadOnlyList<CaptureHistoryEntry> Prepend(
        IReadOnlyList<CaptureHistoryEntry> existing,
        CaptureHistoryEntry entry,
        int maxEntries)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(entry);

        int effectiveMaxEntries = maxEntries <= 0 ? AppConstants.DefaultCaptureHistoryMaxEntries : maxEntries;
        List<CaptureHistoryEntry> entries = [entry];
        entries.AddRange(existing);
        return entries.Take(effectiveMaxEntries).ToList();
    }
}
