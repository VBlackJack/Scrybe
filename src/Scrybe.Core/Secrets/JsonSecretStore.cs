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

namespace Scrybe.Core.Secrets;

/// <summary>
/// JSON store for DPAPI-protected secrets. Only ciphertext and non-secret metadata are written; missing
/// or corrupt files load as an empty vault so Scrybe keeps starting.
/// </summary>
public sealed class JsonSecretStore : ISecretStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    /// <summary>Initializes the store backed by <paramref name="filePath"/>.</summary>
    /// <param name="filePath">Absolute path to the protected secrets JSON file.</param>
    public JsonSecretStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecretEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            await using FileStream stream = File.OpenRead(_filePath);
            List<SecretEntry>? secrets = await JsonSerializer
                .DeserializeAsync<List<SecretEntry>>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            return secrets?.Where(IsValid).ToList() ?? [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            FileLogger.Error($"Failed to read secrets from {_filePath}; starting with an empty vault.", exception);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(IReadOnlyList<SecretEntry> secrets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(secrets);

        try
        {
            await AtomicFileWriter
                .WriteAsync(
                    _filePath,
                    (stream, token) => JsonSerializer.SerializeAsync(stream, secrets, SerializerOptions, token),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            FileLogger.Error($"Failed to save secrets to {_filePath}.", exception);
            return false;
        }
    }

    private static bool IsValid(SecretEntry secret)
        => !string.IsNullOrWhiteSpace(secret.Id)
        && !string.IsNullOrWhiteSpace(secret.Name)
        && !string.IsNullOrWhiteSpace(secret.ProtectedSecret);
}
