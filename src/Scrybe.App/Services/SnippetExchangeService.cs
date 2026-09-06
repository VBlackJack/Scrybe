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
using System.IO;
using Scrybe.App.ViewModels;
using Scrybe.App.Views;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Scrybe.Core.Snippets;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace Scrybe.App.Services;

/// <summary>Windows file selection and explicit import preview around the strict exchange format.</summary>
public sealed class SnippetExchangeService(ILocalizationManager localization)
{
    /// <summary>Exports committed snippets to a new file. Existing files are never overwritten.</summary>
    public async Task<bool> ExportAsync(SnippetLibrary library)
    {
        SaveFileDialog picker = new()
        {
            Title = localization["Exchange.Export"],
            Filter = localization["Exchange.Filter"],
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = false
        };
        if (picker.ShowDialog() != true) { return false; }
        byte[] bytes = await library.ExportAsync();
        await using FileStream output = new(picker.FileName, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await output.WriteAsync(bytes);
        return true;
    }

    /// <summary>Previews every incoming item and applies the selected policy as a single guarded save.</summary>
    public async Task<bool> ImportAsync(SnippetLibrary library)
    {
        OpenFileDialog picker = new() { Title = localization["Exchange.Import"], Filter = localization["Exchange.Filter"] };
        if (picker.ShowDialog() != true) { return false; }
        byte[] bytes;
        await using (FileStream input = new(picker.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            byte[] buffer = new byte[SnippetExchange.MaximumBytes + 1];
            int length = await input.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false);
            if (length > SnippetExchange.MaximumBytes) { throw new InvalidDataException("Import exceeds the size limit."); }
            bytes = buffer.AsSpan(0, length).ToArray();
        }
        IReadOnlyList<Snippet> incoming = SnippetExchange.Import(bytes);
        byte[] snapshot = await library.ExportAsync();
        IReadOnlyList<Snippet> current = SnippetExchange.Import(snapshot);
        int conflicts = incoming.Count(snippet => current.Any(existing => SnippetExchange.Conflicts(existing, snippet)));
        ReviewChoice[] entries = incoming.Select(snippet => new ReviewChoice(
            string.Format(CultureInfo.CurrentCulture, localization["Exchange.Item"], snippet.Name, snippet.Category,
                current.Any(existing => SnippetExchange.Conflicts(existing, snippet)) ? localization["Exchange.Conflict"] : localization["Exchange.New"]),
            snippet)).ToArray();
        ReviewChoice[] policies = Enum.GetValues<SnippetConflictPolicy>()
            .Select(policy => new ReviewChoice(localization["Exchange." + policy], policy)).ToArray();
        DataReviewViewModel review = new(localization["Exchange.Import"],
            string.Format(CultureInfo.CurrentCulture, localization["Exchange.Preview"], incoming.Count, conflicts), entries, policies);
        if (new DataReviewWindow(review).ShowDialog() != true
            || review.SelectedPolicy?.Value is not SnippetConflictPolicy policy) { return false; }
        if (!await library.ImportAsync(snapshot, incoming, policy))
        { throw new IOException("Import was not saved; reload the library and review again."); }
        return true;
    }
}
