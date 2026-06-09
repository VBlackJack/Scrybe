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
using Scrybe.Core.Interfaces;
using Scrybe.Core.IO;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;

namespace Scrybe.Core.Snippets;

/// <summary>
/// JSON-file snippet store. Snippets are non-secret, so they are stored as plain JSON. Loading is robust
/// to a missing or corrupt file: it logs and returns an empty library rather than crashing.
/// </summary>
public sealed class JsonSnippetStore : ISnippetStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    /// <summary>Initializes the store backed by <paramref name="filePath"/>.</summary>
    /// <param name="filePath">Absolute path to the snippets JSON file.</param>
    public JsonSnippetStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Snippet>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            await using FileStream stream = File.OpenRead(_filePath);
            List<Snippet>? snippets = await JsonSerializer
                .DeserializeAsync<List<Snippet>>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            return snippets ?? [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            FileLogger.Error($"Failed to read snippets from {_filePath}; starting with an empty library.", exception);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(IReadOnlyList<Snippet> snippets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snippets);

        try
        {
            await AtomicFileWriter
                .WriteAsync(
                    _filePath,
                    (stream, token) => JsonSerializer.SerializeAsync(stream, snippets, SerializerOptions, token),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            FileLogger.Error($"Failed to save snippets to {_filePath}.", exception);
        }
    }
}
