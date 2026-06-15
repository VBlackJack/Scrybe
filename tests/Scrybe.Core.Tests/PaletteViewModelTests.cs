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
using Scrybe.App.ViewModels;
using Scrybe.Core.Models;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for fast-palette filtering and selection behavior.</summary>
public sealed class PaletteViewModelTests
{
    [Fact]
    public void SnippetPalette_SearchFiltersAndSelectsFirstMatch()
    {
        SnippetPaletteViewModel viewModel = new(
        [
            new Snippet("one", "Package acceptance", "ops", "apt install {{package}}", []),
            new Snippet("two", "Restart service", "ops", "systemctl restart nginx", []),
        ]);

        viewModel.SearchText = "restart";

        viewModel.Snippets.Should().ContainSingle(snippet => snippet.Id == "two");
        viewModel.SelectedSnippet!.Id.Should().Be("two");
        viewModel.IsEmpty.Should().BeFalse();
        viewModel.HasSnippets.Should().BeTrue();
    }

    [Fact]
    public void SecretPalette_SearchFiltersByUsername()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SecretPaletteViewModel viewModel = new(
        [
            new SecretEntry("one", "Lab login", "administrator", "protected", now, now),
            new SecretEntry("two", "Router", "root", "protected", now, now),
        ]);

        viewModel.SearchText = "root";

        viewModel.Secrets.Should().ContainSingle(secret => secret.Id == "two");
        viewModel.SelectedSecret!.Id.Should().Be("two");
        viewModel.CanInject.Should().BeTrue();
    }

    [Fact]
    public void HistoryPalette_SearchKeepsClearAllEnabledForFilteredOutEntries()
    {
        HistoryPaletteViewModel viewModel = new(
        [
            new HistoryPaletteListItem("one", "first capture", "today", 13),
            new HistoryPaletteListItem("two", "second capture", "yesterday", 14),
        ]);

        viewModel.SearchText = "missing";

        viewModel.Entries.Should().BeEmpty();
        viewModel.SelectedEntry.Should().BeNull();
        viewModel.IsEmpty.Should().BeTrue();
        viewModel.HasEntries.Should().BeTrue();
        viewModel.CanCopy.Should().BeFalse();
    }
}
