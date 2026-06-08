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
using Scrybe.Core.Logging;
using Scrybe.Core.Models;

namespace Scrybe.Core.History;

/// <summary>
/// JSON store for DPAPI-protected OCR capture history. Only ciphertext and non-secret metadata are
/// persisted; missing or corrupt files load as an empty history so Scrybe keeps starting.
/// </summary>
public sealed class JsonCaptureHistoryStore : ICaptureHistoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    /// <summary>Initializes the store backed by <paramref name="filePath"/>.</summary>
    /// <param name="filePath">Absolute path to the protected history JSON file.</param>
    public JsonCaptureHistoryStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CaptureHistoryEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            await using FileStream stream = File.OpenRead(_filePath);
            List<CaptureHistoryEntry>? entries = await JsonSerializer
                .DeserializeAsync<List<CaptureHistoryEntry>>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            return entries?.Where(IsValid).ToList() ?? [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            FileLogger.Error($"Failed to read capture history from {_filePath}; starting with an empty history.", exception);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(IReadOnlyList<CaptureHistoryEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using FileStream stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, entries, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            FileLogger.Error($"Failed to save capture history to {_filePath}.", exception);
        }
    }

    private static bool IsValid(CaptureHistoryEntry entry)
        => !string.IsNullOrWhiteSpace(entry.Id)
        && !string.IsNullOrWhiteSpace(entry.ProtectedText)
        && entry.CharCount >= 0;
}
