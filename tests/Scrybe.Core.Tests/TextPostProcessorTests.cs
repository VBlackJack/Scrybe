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
using Scrybe.Core.Text;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>
/// Tests for <see cref="TextPostProcessor"/>. The negative cases (legitimate text left untouched) are
/// as important as the positive cases - together they prove the context-aware gating.
/// </summary>
public sealed class TextPostProcessorTests
{
    private static string Run(string input) => TextPostProcessor.Process(input, TextPostProcessingOptions.Default).Text;

    // --- Positive: confusion correction inside technical tokens ---

    [Fact]
    public void FixesIpToken_LeadingAtSign()
    {
        string result = Run("Error: bind @.0.0.0:8443 permission denied");

        result.Should().Be("Error: bind 0.0.0.0:8443 permission denied");
    }

    [Fact]
    public void FixesGuidToken_Confusions()
    {
        string result = Run("id 550e8400-e29b-41d4-a716-446655440O0O created");

        result.Should().Be("id 550e8400-e29b-41d4-a716-446655440000 created");
    }

    [Fact]
    public void FixesHexToken_Confusions()
    {
        string result = Run("sha deadbeefcafeO00l done");

        result.Should().Be("sha deadbeefcafe0001 done");
    }

    [Fact]
    public void FixesNumericToken_WhenPredominantlyDigits()
    {
        string result = Run("Process 4O21 started");

        result.Should().Be("Process 4021 started");
    }

    // --- Positive: wrap repair and prompt stripping ---

    [Fact]
    public void RepairsWrappedContinuationLine()
    {
        TextPostProcessingResult result = TextPostProcessor.Process(
            "the configuration could not be\nparsed correctly", TextPostProcessingOptions.Default);

        result.Text.Should().Be("the configuration could not be parsed correctly");
        result.MergedLines.Should().Be(1);
    }

    [Fact]
    public void StripsKnownShellPrompts()
    {
        TextPostProcessingResult result = TextPostProcessor.Process(
            "PS C:\\Users\\jb> Get-Process\nroot@server:~# systemctl status\n$ ls -la",
            TextPostProcessingOptions.Default);

        result.Text.Should().Be("Get-Process\nsystemctl status\nls -la");
        result.StrippedPrompts.Should().Be(3);
    }

    // --- Negative: legitimate text must be preserved (proves the gating) ---

    [Fact]
    public void DoesNotTouchEmailAtSign()
    {
        const string input = "Contact john.doe@corp.com for access";

        Run(input).Should().Be(input);
    }

    [Fact]
    public void DoesNotTouchLetterOInWords()
    {
        const string input = "The Code is OK and looks good";

        Run(input).Should().Be(input);
    }

    [Fact]
    public void DoesNotTouchProse()
    {
        const string input = "The server failed to start because the port was already in use.";

        Run(input).Should().Be(input);
    }

    [Fact]
    public void DoesNotMergeStructuredLines()
    {
        TextPostProcessingResult result = TextPostProcessor.Process(
            "Active: failed (Result: exit-code)\nProcess: 4821 running", TextPostProcessingOptions.Default);

        result.Text.Should().Be("Active: failed (Result: exit-code)\nProcess: 4821 running");
        result.MergedLines.Should().Be(0);
    }

    [Fact]
    public void DoesNotCorrectAmbiguousShortLabel()
    {
        // "B5" is half letter, half digit: not predominantly numeric, so it must be left alone.
        Run("Cell B5 holds the total").Should().Be("Cell B5 holds the total");
    }
}
