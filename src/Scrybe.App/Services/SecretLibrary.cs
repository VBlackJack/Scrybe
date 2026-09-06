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

using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;

namespace Scrybe.App.Services;

/// <summary>In-memory secret library backed by a DPAPI-protected store.</summary>
public sealed class SecretLibrary
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly ISecretStore _store;
    private readonly ISecretProtector _protector;
    private readonly List<SecretEntry> _secrets = [];

    /// <summary>Initializes the library with the persistence store and secret protector.</summary>
    /// <param name="store">The protected persistence store.</param>
    /// <param name="protector">The DPAPI protector.</param>
    public SecretLibrary(ISecretStore store, ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(protector);
        _store = store;
        _protector = protector;
    }

    /// <summary>The current protected secret entries.</summary>
    public IReadOnlyList<SecretEntry> Secrets => _secrets;

    /// <summary>Loads the protected secrets from the store.</summary>
    public async Task<bool> LoadAsync()
    {
        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            IReadOnlyList<SecretEntry> loaded = await _store.LoadAsync().ConfigureAwait(false);
            if (_store is IStoreReadState { CanSave: false }) { return false; }
            IReadOnlyList<SecretEntry> current = await MigrateIfNeededAsync(loaded).ConfigureAwait(false);
            _secrets.Clear();
            _secrets.AddRange(current);
            return true;
        }
        finally { _operationGate.Release(); }
    }

    /// <summary>Adds or replaces a secret, protecting new plaintext immediately before persistence.</summary>
    /// <param name="id">Existing id, or <see langword="null"/> for a new secret.</param>
    /// <param name="name">User-facing secret label.</param>
    /// <param name="userName">Optional user/account label.</param>
    /// <param name="secretValue">New plaintext value; blank preserves the existing protected value.</param>
    /// <returns>The saved entry and whether it was persisted.</returns>
    public async Task<SecretSaveResult> SaveAsync(string? id, string name, string? userName, string secretValue)
    {
        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(secretValue);

            SecretEntry? existing = id is null
                ? null
                : _secrets.FirstOrDefault(secret => string.Equals(secret.Id, id, StringComparison.Ordinal));

            if (existing is null && string.IsNullOrEmpty(secretValue))
            {
                throw new InvalidOperationException("A new secret requires a value.");
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            string protectedSecret = string.IsNullOrEmpty(secretValue)
                ? existing!.ProtectedSecret
                : _protector.Protect(secretValue);
            SecretEntry saved = new(
                existing?.Id ?? Guid.NewGuid().ToString("N"),
                name.Trim(),
                string.IsNullOrWhiteSpace(userName) ? null : userName.Trim(),
                protectedSecret,
                existing?.CreatedAtUtc ?? now,
                now);

            List<SecretEntry> pending = new(_secrets);
            int index = pending.FindIndex(secret => string.Equals(secret.Id, saved.Id, StringComparison.Ordinal));
            if (index >= 0)
            {
                pending[index] = saved;
            }
            else
            {
                pending.Add(saved);
            }

            bool persisted = await _store.SaveAsync(pending).ConfigureAwait(false);
            if (persisted) { _secrets.Clear(); _secrets.AddRange(pending); }
            return new SecretSaveResult(saved, persisted);
        }
        finally { _operationGate.Release(); }
    }

    /// <summary>Deletes a secret by id and persists the vault.</summary>
    /// <param name="id">The secret id.</param>
    /// <returns><see langword="true"/> when the deletion was persisted; otherwise <see langword="false"/>.</returns>
    public async Task<bool> DeleteAsync(string id)
    {
        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            List<SecretEntry> pending = new(_secrets);
            pending.RemoveAll(secret => string.Equals(secret.Id, id, StringComparison.Ordinal));
            bool persisted = await _store.SaveAsync(pending).ConfigureAwait(false);
            if (persisted) { _secrets.Clear(); _secrets.AddRange(pending); }
            return persisted;
        }
        finally { _operationGate.Release(); }
    }

    /// <summary>Returns plaintext secret characters for immediate injection, or <see langword="null"/> when absent.</summary>
    /// <param name="id">The secret id.</param>
    public char[]? RevealSecret(string id)
    {
        SecretEntry? secret = _secrets.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
        return secret is null ? null : _protector.UnprotectToChars(secret.ProtectedSecret);
    }

    private async Task<IReadOnlyList<SecretEntry>> MigrateIfNeededAsync(IReadOnlyList<SecretEntry> loaded)
    {
        if (_protector is not IMigratingSecretProtector migratingProtector)
        {
            return loaded;
        }

        List<SecretEntry> migrated = new(loaded.Count);
        bool changed = false;

        foreach (SecretEntry secret in loaded)
        {
            try
            {
                if (migratingProtector.IsProtectedWithCurrentScheme(secret.ProtectedSecret))
                {
                    migrated.Add(secret);
                    continue;
                }

                string upgraded = migratingProtector.ReprotectWithCurrentScheme(secret.ProtectedSecret);
                migrated.Add(secret with { ProtectedSecret = upgraded });
                changed = true;
            }
            catch (Exception exception)
            {
                FileLogger.Error($"Failed to migrate protected secret '{secret.Id}'; keeping the existing value.", exception);
                migrated.Add(secret);
            }
        }

        if (changed)
        {
            bool persisted = await _store.SaveAsync(migrated).ConfigureAwait(false);
            if (!persisted)
            {
                FileLogger.Warn("Protected secret migration completed in memory but could not be persisted.");
            }
        }

        return migrated;
    }
}

/// <summary>The result of a secret save operation.</summary>
/// <param name="Entry">The attempted secret entry, published to the library only when persisted.</param>
/// <param name="Persisted">Whether the entry was written to the protected store.</param>
public sealed record SecretSaveResult(SecretEntry Entry, bool Persisted);
