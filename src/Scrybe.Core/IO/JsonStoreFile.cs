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

/// <summary>
/// Serializes JSON store access and rejects saves after failed reads or stale snapshots. A persistent
/// sibling lock file coordinates cooperating processes; the hash comparison protects loaded state.
/// </summary>
internal sealed class JsonStoreFile<T> where T : class
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _expectedHash;
    private bool _canSave = true;

    public JsonStoreFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public bool CanSave => _canSave;

    public async Task<(T? Value, bool Existed)> LoadAsync(Func<T, bool> validate, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            _canSave = false;
            using FileStream lease = AcquireLease();
            byte[]? bytes = await ReadBytesAsync(token).ConfigureAwait(false);
            if (bytes is null)
            {
                _expectedHash = null;
                _canSave = true;
                return (null, false);
            }

            try
            {
                T? value = JsonSerializer.Deserialize<T>(bytes, Options);
                if (value is null || !validate(value))
                {
                    throw new JsonException("The JSON store contains invalid entries.");
                }

                _expectedHash = Hash(bytes);
                _canSave = true;
                return (value, true);
            }
            catch (JsonException exception)
            {
                FileLogger.Error($"Invalid JSON store '{_path}'; preserving the original payload.", exception);
                _canSave = CorruptJsonQuarantine.TryMoveAside(_path, "store") is not null;
                _expectedHash = null;
                return (null, !_canSave);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            FileLogger.Error($"Could not load '{_path}'; writes are blocked until a successful reload.", exception);
            return (null, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> SaveAsync(T value, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(value);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (!_canSave)
            {
                FileLogger.Warn($"Save blocked for '{_path}'; reload the store before retrying.");
                return false;
            }

            using FileStream lease = AcquireLease();
            byte[]? current = await ReadBytesAsync(token).ConfigureAwait(false);
            if (!string.Equals(_expectedHash, Hash(current), StringComparison.Ordinal))
            {
                _canSave = false;
                FileLogger.Warn($"Save conflict for '{_path}'; the store changed since it was loaded.");
                return false;
            }

            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Options);
            if (current is not null && !current.AsSpan().SequenceEqual(bytes))
            {
                await new StoreBackups(_path).CaptureUnderLeaseAsync(current, token).ConfigureAwait(false);
            }
            await AtomicFileWriter.WriteAsync(
                _path, (stream, cancellation) => stream.WriteAsync(bytes, cancellation).AsTask(), token).ConfigureAwait(false);
            _expectedHash = Hash(bytes);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            FileLogger.Error($"Could not save '{_path}'; original data retained.", exception);
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private FileStream AcquireLease()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        // Do not delete the lock file: unlinking a lock can let different processes lock different files.
        return new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private async Task<byte[]?> ReadBytesAsync(CancellationToken token)
    {
        try
        {
            return await File.ReadAllBytesAsync(_path, token).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static string? Hash(byte[]? bytes) => bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes));
}
