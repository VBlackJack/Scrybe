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
using Scrybe.Core;
using Scrybe.Core.History;
using Scrybe.Core.Models;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for the pure capture-history ring-buffer policy.</summary>
public sealed class CaptureHistoryPolicyTests
{
    [Fact]
    public void Prepend_PutsNewEntryFirst()
    {
        CaptureHistoryEntry existing = Entry("existing");
        CaptureHistoryEntry added = Entry("added");

        IReadOnlyList<CaptureHistoryEntry> result = CaptureHistoryPolicy.Prepend([existing], added, 10);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("added");
        result[1].Id.Should().Be("existing");
    }

    [Fact]
    public void Prepend_DropsOldestEntriesBeyondMax()
    {
        CaptureHistoryEntry oldest = Entry("oldest");
        CaptureHistoryEntry middle = Entry("middle");
        CaptureHistoryEntry newest = Entry("newest");

        IReadOnlyList<CaptureHistoryEntry> result = CaptureHistoryPolicy.Prepend([newest, middle, oldest], Entry("added"), 3);

        result.Select(entry => entry.Id).Should().Equal("added", "newest", "middle");
    }

    [Fact]
    public void Prepend_NonPositiveMaxUsesDefault()
    {
        List<CaptureHistoryEntry> existing = [];
        for (int index = 0; index < AppConstants.DefaultCaptureHistoryMaxEntries + 5; index++)
        {
            existing.Add(Entry(index.ToString()));
        }

        IReadOnlyList<CaptureHistoryEntry> result = CaptureHistoryPolicy.Prepend(existing, Entry("added"), 0);

        result.Should().HaveCount(AppConstants.DefaultCaptureHistoryMaxEntries);
        result[0].Id.Should().Be("added");
    }

    [Fact]
    public void Prepend_NullExistingThrows()
    {
        Action act = () => CaptureHistoryPolicy.Prepend(null!, Entry("added"), 10);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Prepend_NullEntryThrows()
    {
        Action act = () => CaptureHistoryPolicy.Prepend([], null!, 10);

        act.Should().Throw<ArgumentNullException>();
    }

    private static CaptureHistoryEntry Entry(string id) =>
        new(id, "protected-" + id, id.Length, DateTimeOffset.UtcNow);
}
