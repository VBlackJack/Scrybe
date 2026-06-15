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

namespace Scrybe.Core.Settings;

/// <summary>
/// JSON-file settings store. Loading tolerates a missing or corrupt file (logs and returns validated
/// defaults) and missing fields (kept at their defaults); every loaded value is validated and clamped.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    /// <summary>Initializes the store backed by <paramref name="filePath"/>.</summary>
    /// <param name="filePath">Absolute path to the settings JSON file.</param>
    public JsonSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <inheritdoc />
    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new SettingsLoadResult(SettingsValidator.Validate(new AppSettings()), Existed: false);
        }

        try
        {
            await using FileStream stream = File.OpenRead(_filePath);
            AppSettings? settings = await JsonSerializer
                .DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            return new SettingsLoadResult(SettingsValidator.Validate(settings ?? new AppSettings()), Existed: true);
        }
        catch (JsonException exception)
        {
            FileLogger.Error($"Failed to read settings from {_filePath}; falling back to defaults.", exception);
            CorruptJsonQuarantine.TryMoveAside(_filePath, "settings");
            return new SettingsLoadResult(SettingsValidator.Validate(new AppSettings()), Existed: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            FileLogger.Error($"Failed to read settings from {_filePath}; falling back to defaults.", exception);
            return new SettingsLoadResult(SettingsValidator.Validate(new AppSettings()), Existed: false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            await AtomicFileWriter
                .WriteAsync(
                    _filePath,
                    (stream, token) => JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, token),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            FileLogger.Error($"Failed to save settings to {_filePath}.", exception);
            return false;
        }
    }
}
