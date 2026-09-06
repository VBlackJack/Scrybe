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

using System.Security.Cryptography;
using System.Text.Json;
using Scrybe.Core.Logging;

namespace Scrybe.Core.IO;

/// <summary>One local historical version. Payload bytes retain their original encryption.</summary>
public sealed record StoreBackup(string Id, string StoreName, DateTimeOffset CreatedAtUtc, int ByteCount);

/// <summary>Preview tied to both the chosen backup and the exact current file revision.</summary>
public sealed record RestorePreview(StoreBackup Backup, string? CurrentHash, string BackupHash, int EntryCount);

/// <summary>Bounded version history, sharing the stores' interprocess lease and atomic writer.</summary>
public sealed class StoreBackups
{
    /// <summary>Maximum retained versions per store; removed entries may remain until versions expire.</summary>
    public const int RetainedVersions = 20;
    private const int SchemaVersion = 1;
    private const string VersionsSuffix = ".versions";
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;
    private string DirectoryPath => _path + VersionsSuffix;

    /// <summary>Restricts all operations to one explicitly supplied store path.</summary>
    public StoreBackups(string path) => _path = Path.GetFullPath(path);

    internal async Task CaptureUnderLeaseAsync(byte[] bytes, CancellationToken token)
    {
        Directory.CreateDirectory(DirectoryPath);
        string id = Guid.NewGuid().ToString("N") + ".json";
        Envelope envelope = new(SchemaVersion, Path.GetFileName(_path), DateTimeOffset.UtcNow, Hash(bytes), bytes);
        await AtomicFileWriter.WriteAsync(Path.Combine(DirectoryPath, id),
            (stream, cancellation) => JsonSerializer.SerializeAsync(stream, envelope, Options, cancellation), token).ConfigureAwait(false);
        foreach (StoreBackup expired in (await ListAsync(token).ConfigureAwait(false)).Skip(RetainedVersions))
        {
            File.Delete(ResolveId(expired.Id));
        }
    }

    /// <summary>Lists valid versions newest first. Invalid artifacts are preserved and logged.</summary>
    public async Task<IReadOnlyList<StoreBackup>> ListAsync(CancellationToken token = default)
    {
        if (!Directory.Exists(DirectoryPath)) { return []; }
        List<StoreBackup> results = [];
        foreach (string path in Directory.EnumerateFiles(DirectoryPath, "*.json"))
        {
            try
            {
                string id = Path.GetFileName(path);
                Envelope entry = await ReadEnvelopeAsync(id, token).ConfigureAwait(false);
                results.Add(new(id, entry.StoreName, entry.CreatedAtUtc, entry.Payload.Length));
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or ArgumentException)
            {
                FileLogger.Error("Invalid backup retained without offering restoration.", exception);
            }
        }
        return results.OrderByDescending(entry => entry.CreatedAtUtc).ToArray();
    }

    /// <summary>Reads a version and captures the current revision without changing either file.</summary>
    public async Task<RestorePreview> PreviewAsync(string id, CancellationToken token = default)
    {
        Envelope envelope = await ReadEnvelopeAsync(id, token).ConfigureAwait(false);
        int count = StorePayloadValidator.Validate(envelope.StoreName, envelope.Payload);
        byte[]? current = await ReadCurrentAsync(token).ConfigureAwait(false);
        return new(new(id, envelope.StoreName, envelope.CreatedAtUtc, envelope.Payload.Length),
            current is null ? null : Hash(current), envelope.Sha256, count);
    }

    /// <summary>Restores only the exact previewed bytes and current revision; backs up the replaced version first.</summary>
    public async Task RestoreAsync(RestorePreview preview, CancellationToken token = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        using FileStream lease = new(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Envelope envelope = await ReadEnvelopeAsync(preview.Backup.Id, token).ConfigureAwait(false);
        byte[]? current = await ReadCurrentAsync(token).ConfigureAwait(false);
        if (!string.Equals(preview.BackupHash, envelope.Sha256, StringComparison.Ordinal)
            || preview.Backup.StoreName != envelope.StoreName || preview.Backup.CreatedAtUtc != envelope.CreatedAtUtc
            || preview.Backup.ByteCount != envelope.Payload.Length
            || !string.Equals(preview.CurrentHash, current is null ? null : Hash(current), StringComparison.Ordinal))
        {
            throw new IOException("The preview is stale. Review the current version again.");
        }
        StorePayloadValidator.Validate(envelope.StoreName, envelope.Payload);
        if (current is not null) { await CaptureUnderLeaseAsync(current, token).ConfigureAwait(false); }
        await AtomicFileWriter.WriteAsync(_path,
            (stream, cancellation) => stream.WriteAsync(envelope.Payload, cancellation).AsTask(), token).ConfigureAwait(false);
        FileLogger.Info("A previewed store version was restored.");
    }

    private async Task<byte[]?> ReadCurrentAsync(CancellationToken token)
    {
        try { return await File.ReadAllBytesAsync(_path, token).ConfigureAwait(false); }
        catch (FileNotFoundException) { return null; }
    }

    private string ResolveId(string id)
    {
        if (!id.EndsWith(".json", StringComparison.Ordinal) || !Guid.TryParseExact(id[..^5], "N", out _))
        { throw new ArgumentException("Invalid backup identifier.", nameof(id)); }
        return Path.Combine(DirectoryPath, id);
    }

    private async Task<Envelope> ReadEnvelopeAsync(string id, CancellationToken token)
    {
        byte[] bytes = await File.ReadAllBytesAsync(ResolveId(id), token).ConfigureAwait(false);
        Envelope? entry = JsonSerializer.Deserialize<Envelope>(bytes);
        if (entry is null || entry.Version != SchemaVersion || entry.Payload is null
            || entry.StoreName != Path.GetFileName(_path) || Hash(entry.Payload) != entry.Sha256)
        { throw new InvalidDataException("Backup integrity or schema validation failed."); }
        return entry;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private sealed record Envelope(int Version, string StoreName, DateTimeOffset CreatedAtUtc, string Sha256, byte[] Payload);
}
