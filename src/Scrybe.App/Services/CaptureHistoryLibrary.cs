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

using Scrybe.Core.History;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;
using Scrybe.Core.Security;

namespace Scrybe.App.Services;

/// <summary>In-memory OCR capture history backed by a DPAPI-protected store.</summary>
public sealed class CaptureHistoryLibrary
{
    private readonly ICaptureHistoryStore _store;
    private readonly ISecretProtector _protector;
    private readonly AppSettings _settings;
    private readonly List<CaptureHistoryEntry> _entries = [];

    /// <summary>Initializes the history library with the persistence store and secret protector.</summary>
    /// <param name="store">The protected persistence store.</param>
    /// <param name="protector">The DPAPI protector.</param>
    /// <param name="settings">Live application settings.</param>
    public CaptureHistoryLibrary(ICaptureHistoryStore store, ISecretProtector protector, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(settings);

        _store = store;
        _protector = protector;
        _settings = settings;
    }

    /// <summary>Raised after entries are loaded or mutated.</summary>
    public event EventHandler? EntriesChanged;

    /// <summary>The current protected history entries, newest first.</summary>
    public IReadOnlyList<CaptureHistoryEntry> Entries => _entries;

    /// <summary>Loads the protected history from the store.</summary>
    public async Task LoadAsync()
    {
        IReadOnlyList<CaptureHistoryEntry> loaded = await _store.LoadAsync().ConfigureAwait(false);
        _entries.Clear();
        _entries.AddRange(loaded);
        EntriesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds a plaintext OCR result to the protected history and persists the capped ring buffer.</summary>
    /// <param name="text">OCR text to protect and persist.</param>
    public async Task AddAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        CaptureHistoryEntry entry = new(
            Guid.NewGuid().ToString("N"),
            _protector.Protect(text),
            text.Length,
            DateTimeOffset.UtcNow);

        IReadOnlyList<CaptureHistoryEntry> updated = CaptureHistoryPolicy.Prepend(
            _entries,
            entry,
            _settings.CaptureHistoryMaxEntries);

        _entries.Clear();
        _entries.AddRange(updated);
        await _store.SaveAsync(_entries).ConfigureAwait(false);
        EntriesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reveals plaintext for one entry, or <see langword="null"/> when the entry is absent.</summary>
    /// <param name="id">The history entry id.</param>
    public string? RevealText(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        CaptureHistoryEntry? entry = _entries.FirstOrDefault(
            candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        if (entry is null)
        {
            return null;
        }

        char[]? chars = null;
        try
        {
            chars = _protector.UnprotectToChars(entry.ProtectedText);
            return new string(chars);
        }
        finally
        {
            SecretMemory.Clear(chars);
        }
    }

    /// <summary>Replaces one entry's protected plaintext while preserving its identity and capture timestamp.</summary>
    /// <param name="id">The history entry id.</param>
    /// <param name="newText">The new plaintext to protect and persist.</param>
    public async Task UpdateAsync(string id, string newText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (string.IsNullOrWhiteSpace(newText))
        {
            return;
        }

        int index = _entries.FindIndex(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            FileLogger.Warn($"Capture history update requested for missing entry '{id}'.");
            return;
        }

        CaptureHistoryEntry current = _entries[index];
        CaptureHistoryEntry updated = new(
            current.Id,
            _protector.Protect(newText),
            newText.Length,
            current.CapturedAtUtc);

        _entries[index] = updated;
        await _store.SaveAsync(_entries).ConfigureAwait(false);
        EntriesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clears every history entry and persists an empty history.</summary>
    public async Task ClearAsync()
    {
        _entries.Clear();
        await _store.SaveAsync(_entries).ConfigureAwait(false);
        EntriesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Deletes one history entry by id and persists the remaining entries.</summary>
    /// <param name="id">The history entry id.</param>
    public async Task DeleteAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _entries.RemoveAll(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
        await _store.SaveAsync(_entries).ConfigureAwait(false);
        EntriesChanged?.Invoke(this, EventArgs.Empty);
    }
}
