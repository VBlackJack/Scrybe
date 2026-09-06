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

using Scrybe.App.Services;
using Scrybe.Core.History;
using Scrybe.Core.Models;
using Scrybe.Core.Secrets;
using Scrybe.Core.Security;
using Scrybe.Core.Settings;
using Scrybe.Core.Snippets;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Regression coverage for unreadable, malformed and stale JSON snapshots.</summary>
public sealed class StoreAdmissionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ScrybeStoreAdmissionTests", Guid.NewGuid().ToString("N"));

    public StoreAdmissionTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task LockedVault_RejectsSaveUntilReload_AndPreservesExistingSecret()
    {
        string path = Path.Combine(_directory, "secrets.json");
        SecretLibrary seed = new(new JsonSecretStore(path), new DpapiSecretProtector());
        Assert.True((await seed.SaveAsync(null, "existing", null, "synthetic-one")).Persisted);
        byte[] original = await File.ReadAllBytesAsync(path);
        SecretLibrary restarted = new(new JsonSecretStore(path), new DpapiSecretProtector());
        using (FileStream held = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await restarted.LoadAsync();
        }
        Assert.False((await restarted.SaveAsync(null, "new", null, "synthetic-two")).Persisted);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        await restarted.LoadAsync();
        Assert.True((await restarted.SaveAsync(null, "new", null, "synthetic-two")).Persisted);
        Assert.Equal(2, (await new JsonSecretStore(path).LoadAsync()).Count);
    }

    [Fact]
    public async Task TwoLibraries_RejectStaleSave_AndAllowExplicitReload()
    {
        string path = Path.Combine(_directory, "secrets.json");
        SecretLibrary first = new(new JsonSecretStore(path), new DpapiSecretProtector());
        SecretLibrary second = new(new JsonSecretStore(path), new DpapiSecretProtector());
        await first.LoadAsync();
        await second.LoadAsync();
        Assert.True((await first.SaveAsync(null, "first", null, "synthetic-one")).Persisted);
        byte[] original = await File.ReadAllBytesAsync(path);
        Assert.False((await second.SaveAsync(null, "second", null, "synthetic-two")).Persisted);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        await second.LoadAsync();
        Assert.True((await second.SaveAsync(null, "second", null, "synthetic-two")).Persisted);
        Assert.Equal(2, (await new JsonSecretStore(path).LoadAsync()).Count);
    }

    [Theory]
    [InlineData("secrets")]
    [InlineData("history")]
    [InlineData("snippets")]
    [InlineData("settings")]
    public async Task AllStores_RejectUnreadableAndStaleWrites(string kind)
    {
        string path = Path.Combine(_directory, kind + ".json");
        StoreHarness seed = Create(kind, path);
        Assert.True(await seed.Save());
        byte[] original = await File.ReadAllBytesAsync(path);
        StoreHarness blocked = Create(kind, path);
        using (FileStream held = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await blocked.Load();
        }
        Assert.False(await blocked.Save());
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        await blocked.Load();
        // Whitespace changes represent an external revision too.
        await File.AppendAllTextAsync(path, " ");
        byte[] changed = await File.ReadAllBytesAsync(path);
        Assert.False(await blocked.Save());
        Assert.Equal(changed, await File.ReadAllBytesAsync(path));
        await blocked.Load();
        Assert.True(await blocked.Save());
    }

    [Theory]
    [InlineData("secrets")]
    [InlineData("history")]
    [InlineData("snippets")]
    public async Task NullListElement_IsQuarantinedByteForByteWithoutCrash(string kind)
    {
        string path = Path.Combine(_directory, kind + ".json");
        await File.WriteAllTextAsync(path, "[null]");
        StoreHarness store = Create(kind, path);
        await store.Load();
        Assert.False(File.Exists(path));
        string backup = Assert.Single(Directory.GetFiles(_directory, kind + ".json.corrupt.*.json"));
        Assert.Equal("[null]", await File.ReadAllTextAsync(backup));
        Assert.True(await store.Save());
    }

    [Fact]
    public async Task SharedLease_PreventsCooperatingWriterFromEntering()
    {
        string path = Path.Combine(_directory, "settings.json");
        JsonSettingsStore store = new(path);
        using (FileStream held = new(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(await store.SaveAsync(new AppSettings()));
            Assert.False(File.Exists(path));
        }
        Assert.True(await store.SaveAsync(new AppSettings()));
    }

    [Fact]
    public async Task FailedQuarantine_KeepsOriginalAndBlocksSubsequentSave()
    {
        string path = Path.Combine(_directory, "secrets.json");
        await File.WriteAllTextAsync(path, "[null]");
        JsonSecretStore store = new(path);
        // Permit reading but deny the delete/rename needed by quarantine.
        using (FileStream held = new(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Empty(await store.LoadAsync());
        }
        Assert.False(await store.SaveAsync([]));
        Assert.Equal("[null]", await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.GetFiles(_directory, "*.corrupt.*.json"));
        await store.LoadAsync();
        Assert.True(await store.SaveAsync([]));
        Assert.Single(Directory.GetFiles(_directory, "*.corrupt.*.json"));
    }

    private static StoreHarness Create(string kind, string path)
    {
        switch (kind)
        {
            case "settings":
                JsonSettingsStore settings = new(path);
                return new(async () => { await settings.LoadAsync(); }, () => settings.SaveAsync(new AppSettings()));
            case "secrets":
                JsonSecretStore secrets = new(path);
                return new(async () => { await secrets.LoadAsync(); }, () => secrets.SaveAsync([]));
            case "history":
                JsonCaptureHistoryStore history = new(path);
                return new(async () => { await history.LoadAsync(); }, () => history.SaveAsync([]));
            default:
                JsonSnippetStore snippets = new(path);
                return new(async () => { await snippets.LoadAsync(); }, () => snippets.SaveAsync([]));
        }
    }

    private sealed record StoreHarness(Func<Task> Load, Func<Task<bool>> Save);

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
