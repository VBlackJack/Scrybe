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
using Scrybe.Core.Models;
using Scrybe.Core.Text;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>
/// Tests for the OCR cleanup modes. The Log Cleaner negative cases (version / IP / mid-line bracket /
/// time-in-path / prose left untouched) are as important as the positives - they prove the stripping
/// is conservative and anchored.
/// </summary>
public sealed class SmartModesTests
{
    private static string Clean(string input, OcrCleanupMode mode)
        => TextPostProcessor.Process(input, TextPostProcessingOptions.ForMode(mode)).Text;

    [Fact]
    public void Raw_IsByteForByteIdentity()
    {
        const string input = "  2026-06-07 12:00:00 INFO svc[42]: hi\r\n  $ ls -la\r\n";

        string result = Clean(input, OcrCleanupMode.Raw);

        result.Should().Be(input);
    }

    [Fact]
    public void Standard_AppliesTheConfusionFix()
    {
        string result = Clean("Error: bind @.0.0.0:8443 permission denied", OcrCleanupMode.Standard);

        result.Should().Be("Error: bind 0.0.0.0:8443 permission denied");
    }

    [Fact]
    public void LogCleaner_StripsTimestampsLevelsAndPids()
    {
        const string input =
            "2026-06-07 12:00:00 INFO nginx[1234]: started\n"
            + "[12:00:00.123] WARNING disk space low\n"
            + "Jun  7 08:15:30 ERROR connection lost";

        string result = Clean(input, OcrCleanupMode.LogCleaner);

        result.Should().Be("nginx: started\ndisk space low\nconnection lost");
    }

    [Theory]
    [InlineData("Released version 1.2.3 to production")]
    [InlineData("Host 10.0.0.1 is online")]
    [InlineData("build [42] passed all checks")]
    [InlineData("open /var/log/12:00:00/archive now")]
    [InlineData("The maintenance window is at 12:00:00 sharp")]
    public void LogCleaner_DoesNotDamageLegitimateContent(string input)
    {
        string result = Clean(input, OcrCleanupMode.LogCleaner);

        result.Should().Be(input);
    }

    [Fact]
    public void CodeFormatter_PreservesIndentationAndDoesNotMergeLines()
    {
        const string input = "function deploy() {\n    echo starting\n    return 0\n}";

        TextPostProcessingResult result = TextPostProcessor.Process(input, TextPostProcessingOptions.CodeFormatter);

        result.Text.Should().Be(input);
        result.MergedLines.Should().Be(0);
    }

    [Fact]
    public void CodeFormatter_DoesNotMergeLowercaseContinuation()
    {
        const string input = "the value is\n    derived from the config";

        TextPostProcessingResult result = TextPostProcessor.Process(input, TextPostProcessingOptions.CodeFormatter);

        result.MergedLines.Should().Be(0);
        result.Text.Should().Be(input);
    }
}
