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

using System.Text.Json;
using System.Text.Json.Serialization;
using Scrybe.Core.Models;

namespace Scrybe.Core.Snippets;

/// <summary>Explicit conflict policy shown before importing any snippet.</summary>
public enum SnippetConflictPolicy { KeepExisting, ReplaceExisting, ImportCopies }

/// <summary>Versioned interchange restricted to non-secret snippet templates.</summary>
public static class SnippetExchange
{
    /// <summary>Bounded import size to keep previews responsive.</summary>
    public const int MaximumBytes = 4 * 1024 * 1024;
    /// <summary>Maximum records accepted in one exchange.</summary>
    public const int MaximumEntries = 1000;
    private const int Version = 1;
    private const string Format = "Scrybe.Snippets";
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>Produces a strict envelope with no access to secret or history stores.</summary>
    public static byte[] Export(IReadOnlyList<Snippet> snippets)
    {
        Validate(snippets);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new Envelope(Format, Version, snippets), Options);
        if (bytes.Length > MaximumBytes) { throw new InvalidDataException("Snippet export exceeds the size limit."); }
        return bytes;
    }

    /// <summary>Rejects unknown formats, fields, versions, duplicate IDs and malformed records.</summary>
    public static IReadOnlyList<Snippet> Import(byte[] bytes)
    {
        if (bytes.Length > MaximumBytes) { throw new InvalidDataException("Snippet import exceeds the size limit."); }
        using (JsonDocument document = JsonDocument.Parse(bytes)) { RejectDuplicateProperties(document.RootElement); }
        Envelope? envelope = JsonSerializer.Deserialize<Envelope>(bytes, Options);
        if (envelope is null || envelope.Format != Format || envelope.Version != Version || envelope.Snippets is null)
        { throw new InvalidDataException("Unsupported snippet exchange format."); }
        Validate(envelope.Snippets);
        return envelope.Snippets;
    }

    /// <summary>Validates collection identities and parameter definitions before persistence.</summary>
    public static void Validate(IReadOnlyList<Snippet> snippets)
    {
        if (snippets.Count > MaximumEntries || snippets.Any(snippet => snippet is null
            || string.IsNullOrWhiteSpace(snippet.Id) || string.IsNullOrWhiteSpace(snippet.Name)
            || snippet.Template is null || snippet.Parameters is null
            || snippet.Parameters.Any(parameter => parameter is null || string.IsNullOrWhiteSpace(parameter.Name) || parameter.Label is null)
            || snippet.Parameters.Select(parameter => parameter.Name).Distinct(StringComparer.Ordinal).Count() != snippet.Parameters.Count)
            || snippets.Select(snippet => snippet.Id).Distinct(StringComparer.Ordinal).Count() != snippets.Count)
        { throw new InvalidDataException("Invalid snippet identities or parameters."); }
    }

    /// <summary>Detects an ID collision or the same display name and category.</summary>
    public static bool Conflicts(Snippet left, Snippet right) => left.Id == right.Id
        || (string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Category ?? string.Empty, right.Category ?? string.Empty, StringComparison.OrdinalIgnoreCase));

    /// <summary>Builds a complete prospective library; publication remains one store save.</summary>
    public static IReadOnlyList<Snippet> Merge(IReadOnlyList<Snippet> current, IReadOnlyList<Snippet> incoming, SnippetConflictPolicy policy)
    {
        Validate(current); Validate(incoming);
        if (!Enum.IsDefined(policy)) { throw new ArgumentOutOfRangeException(nameof(policy)); }
        List<Snippet> pending = new(current);
        foreach (Snippet snippet in incoming)
        {
            bool conflict = pending.Any(existing => Conflicts(existing, snippet));
            if (conflict && policy == SnippetConflictPolicy.KeepExisting) { continue; }
            if (conflict && policy == SnippetConflictPolicy.ReplaceExisting)
            {
                // Ambiguous matches must not delete multiple independently maintained records.
                Snippet[] matches = pending.Where(existing => Conflicts(existing, snippet)).ToArray();
                if (matches.Length != 1) { throw new InvalidDataException("Ambiguous snippet replacement."); }
                pending[pending.IndexOf(matches[0])] = snippet with { Id = matches[0].Id };
            }
            else { pending.Add(conflict ? snippet with { Id = Guid.NewGuid().ToString("N") } : snippet); }
        }
        Validate(pending);
        return pending;
    }

    private sealed record Envelope(string Format, int Version, IReadOnlyList<Snippet> Snippets);

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) { throw new InvalidDataException("Duplicate JSON property."); }
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray()) { RejectDuplicateProperties(item); }
        }
    }
}
