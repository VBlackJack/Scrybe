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

using FluentAssertions;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Scrybe.Core.Secrets;
using Scrybe.Core.Security;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for DPAPI secret protection and the encrypted JSON vault store.</summary>
public sealed class SecretVaultTests
{
    private const string PlainSecret = "ScrybeSecret#2026!";

    [Fact]
    public void DpapiProtector_RoundTrip_RestoresSecretWithoutPlainCiphertext()
    {
        ISecretProtector protector = new DpapiSecretProtector();

        string protectedSecret = protector.Protect(PlainSecret);
        char[] restored = protector.UnprotectToChars(protectedSecret);

        try
        {
            restored.Should().Equal(PlainSecret.ToCharArray());
            protectedSecret.Should().NotContain(PlainSecret);
        }
        finally
        {
            SecretMemory.Clear(restored);
        }

        restored.Should().OnlyContain(character => character == '\0');
    }

    [Fact]
    public async Task JsonSecretStore_RoundTrip_DoesNotPersistPlaintext()
    {
        string path = CreateTempPath();
        try
        {
            ISecretProtector protector = new DpapiSecretProtector();
            ISecretStore store = new JsonSecretStore(path);
            SecretEntry saved = new(
                "secret-1",
                "Lab password",
                "administrator",
                protector.Protect(PlainSecret),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);

            await store.SaveAsync([saved]);
            string rawJson = await File.ReadAllTextAsync(path);
            IReadOnlyList<SecretEntry> loaded = await store.LoadAsync();

            rawJson.Should().NotContain(PlainSecret);
            loaded.Should().ContainSingle();
            char[] restored = protector.UnprotectToChars(loaded[0].ProtectedSecret);
            try
            {
                restored.Should().Equal(PlainSecret.ToCharArray());
            }
            finally
            {
                SecretMemory.Clear(restored);
            }
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task JsonSecretStore_CorruptFile_LoadsEmptyWithoutThrowing()
    {
        string path = CreateTempPath();
        await File.WriteAllTextAsync(path, "{ not valid secrets json ]");
        try
        {
            ISecretStore store = new JsonSecretStore(path);

            IReadOnlyList<SecretEntry> loaded = await store.LoadAsync();

            loaded.Should().BeEmpty();
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static string CreateTempPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ScrybeSecretTests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
