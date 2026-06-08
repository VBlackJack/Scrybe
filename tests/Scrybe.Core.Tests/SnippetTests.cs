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
using Scrybe.Core.Snippets;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for snippet template substitution and the JSON snippet store.</summary>
public sealed class SnippetTests
{
    private static Snippet SshSnippet { get; } = new(
        "ssh",
        "SSH login",
        "Network",
        "ssh {{user}}@{{host}}",
        [new SnippetParameter("user", "User", null), new SnippetParameter("host", "Host", "localhost")]);

    [Fact]
    public void Render_AllParametersFilled_ResolvesAndIsComplete()
    {
        TemplateRenderResult result = TemplateRenderer.Render(
            SshSnippet,
            new Dictionary<string, string?> { ["user"] = "jb", ["host"] = "srv01" });

        result.Text.Should().Be("ssh jb@srv01");
        result.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Render_MissingParameter_IsReportedAndPlaceholderKept()
    {
        TemplateRenderResult result = TemplateRenderer.Render(
            new Snippet("s", "s", null, "ssh {{user}}@{{host}}", [new SnippetParameter("user", "User", null), new SnippetParameter("host", "Host", null)]),
            new Dictionary<string, string?> { ["user"] = "jb" });

        result.IsComplete.Should().BeFalse();
        result.MissingParameters.Should().ContainSingle().Which.Should().Be("host");
        result.Text.Should().Be("ssh jb@{{host}}");
    }

    [Fact]
    public void Render_AppliesDefaultWhenValueNotProvided()
    {
        TemplateRenderResult result = TemplateRenderer.Render(
            SshSnippet,
            new Dictionary<string, string?> { ["user"] = "jb" });

        result.Text.Should().Be("ssh jb@localhost");
        result.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Render_NoParameterSnippet_PassesTemplateThrough()
    {
        Snippet snippet = new("p", "Pause", null, "racadm serveraction powercycle", []);

        TemplateRenderResult result = TemplateRenderer.Render(snippet, new Dictionary<string, string?>());

        result.Text.Should().Be("racadm serveraction powercycle");
        result.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Render_EscapedBraces_BecomeLiteralBraces()
    {
        TemplateRenderResult result = TemplateRenderer.Render(
            "echo {{{{literal}}}} and {{value}}",
            new Dictionary<string, string?> { ["value"] = "X" });

        result.Text.Should().Be("echo {{literal}} and X");
        result.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task JsonStore_RoundTrip_PreservesSnippets()
    {
        string path = CreateTempPath();
        try
        {
            ISnippetStore store = new JsonSnippetStore(path);
            IReadOnlyList<Snippet> saved = [SshSnippet];

            await store.SaveAsync(saved);
            IReadOnlyList<Snippet> loaded = await store.LoadAsync();

            loaded.Should().BeEquivalentTo(saved);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task JsonStore_MissingFile_LoadsEmpty()
    {
        ISnippetStore store = new JsonSnippetStore(CreateTempPath());

        IReadOnlyList<Snippet> loaded = await store.LoadAsync();

        loaded.Should().BeEmpty();
    }

    [Fact]
    public async Task JsonStore_CorruptFile_LoadsEmptyWithoutThrowing()
    {
        string path = CreateTempPath();
        await File.WriteAllTextAsync(path, "{ this is not valid json ][");
        try
        {
            ISnippetStore store = new JsonSnippetStore(path);

            IReadOnlyList<Snippet> loaded = await store.LoadAsync();

            loaded.Should().BeEmpty();
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static string CreateTempPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ScrybeSnippetTests");
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
