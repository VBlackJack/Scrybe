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
using Scrybe.Core.History;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Scrybe.Core.Security;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for the encrypted capture-history JSON store.</summary>
public sealed class CaptureHistoryStoreTests
{
    private const string PlainText = "Scrybe capture history plaintext 2026";

    [Fact]
    public async Task JsonCaptureHistoryStore_RoundTrip_DoesNotPersistPlaintext()
    {
        string path = CreateTempPath();
        try
        {
            ISecretProtector protector = new DpapiSecretProtector();
            ICaptureHistoryStore store = new JsonCaptureHistoryStore(path);
            CaptureHistoryEntry saved = new(
                "history-1",
                protector.Protect(PlainText),
                PlainText.Length,
                DateTimeOffset.UtcNow);

            await store.SaveAsync([saved]);
            string rawJson = await File.ReadAllTextAsync(path);
            IReadOnlyList<CaptureHistoryEntry> loaded = await store.LoadAsync();

            EnumerateAtomicTempFiles(path).Should().BeEmpty();
            rawJson.Should().NotContain(PlainText);
            loaded.Should().ContainSingle();
            char[] restored = protector.UnprotectToChars(loaded[0].ProtectedText);
            try
            {
                restored.Should().Equal(PlainText.ToCharArray());
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
    public async Task JsonCaptureHistoryStore_CorruptFile_LoadsEmptyWithoutThrowing()
    {
        string path = CreateTempPath();
        const string CorruptJson = "{ not valid history json ]";
        await File.WriteAllTextAsync(path, CorruptJson);
        try
        {
            ICaptureHistoryStore store = new JsonCaptureHistoryStore(path);

            IReadOnlyList<CaptureHistoryEntry> loaded = await store.LoadAsync();

            loaded.Should().BeEmpty();
            File.Exists(path).Should().BeFalse();
            IReadOnlyList<string> quarantined = EnumerateQuarantinedFiles(path);
            quarantined.Should().ContainSingle();
            File.ReadAllText(quarantined[0]).Should().Be(CorruptJson);
        }
        finally
        {
            TryDelete(path);
            TryDeleteQuarantined(path);
        }
    }

    private static string CreateTempPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ScrybeCaptureHistoryTests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
    }

    private static IReadOnlyList<string> EnumerateAtomicTempFiles(string path)
    {
        string directory = Path.GetDirectoryName(path)!;
        string pattern = "." + Path.GetFileName(path) + ".*.tmp";
        return Directory.EnumerateFiles(directory, pattern).ToList();
    }

    private static IReadOnlyList<string> EnumerateQuarantinedFiles(string path)
    {
        string directory = Path.GetDirectoryName(path)!;
        string pattern = Path.GetFileName(path) + ".corrupt.*.json";
        return Directory.EnumerateFiles(directory, pattern).ToList();
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

    private static void TryDeleteQuarantined(string path)
    {
        foreach (string quarantined in EnumerateQuarantinedFiles(path))
        {
            TryDelete(quarantined);
        }
    }
}
