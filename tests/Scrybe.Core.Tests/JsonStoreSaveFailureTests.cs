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
using Scrybe.Core.Models;
using Scrybe.Core.Secrets;
using Scrybe.Core.Settings;
using Scrybe.Core.Snippets;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Verifies that JSON store save failures are observable and keep existing files intact.</summary>
public sealed class JsonStoreSaveFailureTests
{
    private const string OriginalContent = "original store content";

    /// <summary>Store save delegates used by the shared success and failure contract tests.</summary>
    public static TheoryData<string, Func<string, Task<bool>>> StoreSaves => new()
    {
        { "settings", path => new JsonSettingsStore(path).SaveAsync(new AppSettings()) },
        { "snippets", path => new JsonSnippetStore(path).SaveAsync([new Snippet("snippet-1", "Snippet", null, "{{value}}", [])]) },
        { "secrets", path => new JsonSecretStore(path).SaveAsync([CreateSecretEntry()]) },
        { "history", path => new JsonCaptureHistoryStore(path).SaveAsync([CreateHistoryEntry()]) },
    };

    [Theory]
    [MemberData(nameof(StoreSaves))]
    public async Task JsonStore_SaveAsync_ReturnsTrueOnSuccess(string storeName, Func<string, Task<bool>> saveAsync)
    {
        string path = CreateTempPath(storeName);
        try
        {
            bool saved = await saveAsync(path);

            saved.Should().BeTrue();
            File.Exists(path).Should().BeTrue();
            EnumerateAtomicTempFiles(path).Should().BeEmpty();
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Theory]
    [MemberData(nameof(StoreSaves))]
    public async Task JsonStore_SaveAsync_WhenMoveFails_ReturnsFalseAndKeepsExistingFile(
        string storeName,
        Func<string, Task<bool>> saveAsync)
    {
        string path = CreateTempPath(storeName);
        try
        {
            await File.WriteAllTextAsync(path, OriginalContent);
            File.SetAttributes(path, FileAttributes.ReadOnly);

            bool saved = await saveAsync(path);

            File.SetAttributes(path, FileAttributes.Normal);
            saved.Should().BeFalse();
            string currentContent = await File.ReadAllTextAsync(path);
            currentContent.Should().Be(OriginalContent);
            EnumerateAtomicTempFiles(path).Should().BeEmpty();
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    private static SecretEntry CreateSecretEntry()
        => new(
            "secret-1",
            "Secret",
            "user",
            "protected-secret",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static CaptureHistoryEntry CreateHistoryEntry()
        => new(
            "history-1",
            "protected-history",
            17,
            DateTimeOffset.UtcNow);

    private static string CreateTempPath(string storeName)
    {
        string directory = Path.Combine(Path.GetTempPath(), "ScrybeJsonStoreSaveFailureTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, storeName + ".json");
    }

    private static IReadOnlyList<string> EnumerateAtomicTempFiles(string path)
    {
        string directory = Path.GetDirectoryName(path)!;
        string pattern = "." + Path.GetFileName(path) + ".*.tmp";
        return Directory.EnumerateFiles(directory, pattern).ToList();
    }

    private static void DeleteTempDirectory(string path)
    {
        string directory = Path.GetDirectoryName(path)!;
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(directory))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
