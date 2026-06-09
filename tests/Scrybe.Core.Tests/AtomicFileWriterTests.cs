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

using System.Text;
using System.Text.Json;
using FluentAssertions;
using Scrybe.Core.IO;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for crash-safe same-directory atomic file writes.</summary>
public sealed class AtomicFileWriterTests
{
    [Fact]
    public async Task WriteAsync_FirstWrite_CreatesDestinationAndLeavesNoTempFile()
    {
        string path = CreateTempPath();
        try
        {
            await AtomicFileWriter.WriteAsync(
                path,
                (stream, token) => WriteStringAsync(stream, "first", token));

            File.ReadAllText(path).Should().Be("first");
            EnumerateTempFiles(path).Should().BeEmpty();
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task WriteAsync_RoundTripJson_LoadsBackIdentically()
    {
        string path = CreateTempPath();
        try
        {
            SampleDocument saved = new("Scrybe", 30);

            await AtomicFileWriter.WriteAsync(
                path,
                (stream, token) => JsonSerializer.SerializeAsync(stream, saved, cancellationToken: token));
            await using FileStream stream = File.OpenRead(path);
            SampleDocument? loaded = await JsonSerializer.DeserializeAsync<SampleDocument>(stream);

            loaded.Should().Be(saved);
            EnumerateTempFiles(path).Should().BeEmpty();
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task WriteAsync_ExistingFile_ReplacesDestinationAndLeavesNoTempFile()
    {
        string path = CreateTempPath();
        await File.WriteAllTextAsync(path, "original");
        try
        {
            await AtomicFileWriter.WriteAsync(
                path,
                (stream, token) => WriteStringAsync(stream, "replacement", token));

            File.ReadAllText(path).Should().Be("replacement");
            EnumerateTempFiles(path).Should().BeEmpty();
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task WriteAsync_FailedWrite_KeepsExistingDestinationAndDeletesTempFile()
    {
        string path = CreateTempPath();
        await File.WriteAllTextAsync(path, "original");
        try
        {
            Func<Task> act = () => AtomicFileWriter.WriteAsync(
                path,
                async (stream, token) =>
                {
                    await WriteStringAsync(stream, "partial", token).ConfigureAwait(false);
                    throw new InvalidOperationException("Synthetic write failure.");
                });

            await act.Should().ThrowAsync<InvalidOperationException>();
            File.ReadAllText(path).Should().Be("original");
            EnumerateTempFiles(path).Should().BeEmpty();
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static Task WriteStringAsync(Stream stream, string content, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        return stream.WriteAsync(bytes.AsMemory(), cancellationToken).AsTask();
    }

    private static IReadOnlyList<string> EnumerateTempFiles(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            directory = Directory.GetCurrentDirectory();
        }

        string pattern = "." + Path.GetFileName(path) + ".*.tmp";
        return Directory.EnumerateFiles(directory, pattern).ToList();
    }

    private static string CreateTempPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ScrybeAtomicFileWriterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "data.json");
    }

    private static void TryDelete(string path)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private sealed record SampleDocument(string Name, int Version);
}
