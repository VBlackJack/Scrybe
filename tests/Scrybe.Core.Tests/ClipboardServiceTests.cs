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
using Scrybe.Core.Interfaces;
using Scrybe.Core.Services;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for the retry/no-throw behavior of <see cref="ClipboardService"/> using a fake writer.</summary>
public sealed class ClipboardServiceTests
{
    [Fact]
    public async Task SetTextAsync_RetriesUntilSuccess()
    {
        FakeClipboardWriter writer = new(failuresBeforeSuccess: 2);
        ClipboardService service = new(writer);

        await service.SetTextAsync("payload");

        writer.Calls.Should().Be(3);
        writer.LastText.Should().Be("payload");
    }

    [Fact]
    public async Task SetTextAsync_WhenAllAttemptsFail_DoesNotThrowAndStopsAtRetryLimit()
    {
        FakeClipboardWriter writer = new(failuresBeforeSuccess: int.MaxValue);
        ClipboardService service = new(writer);

        Func<Task> act = async () => await service.SetTextAsync("payload");

        await act.Should().NotThrowAsync();
        writer.Calls.Should().Be(AppConstants.ClipboardRetryCount);
    }

    private sealed class FakeClipboardWriter : IClipboardWriter
    {
        private readonly int _failuresBeforeSuccess;

        public FakeClipboardWriter(int failuresBeforeSuccess) => _failuresBeforeSuccess = failuresBeforeSuccess;

        public int Calls { get; private set; }

        public string? LastText { get; private set; }

        public void SetText(string text)
        {
            Calls++;
            LastText = text;

            if (Calls <= _failuresBeforeSuccess)
            {
                throw new InvalidOperationException("Clipboard is busy.");
            }
        }
    }
}
