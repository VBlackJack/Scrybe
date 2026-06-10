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
using Scrybe.Core.Startup;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for the pure autostart command formatting and parsing helpers.</summary>
public sealed class StartupCommandTests
{
    [Fact]
    public void Format_WrapsPathInQuotes()
    {
        string command = StartupCommand.Format(@"C:\dir with space\Scrybe.exe");

        command.Should().Be("\"C:\\dir with space\\Scrybe.exe\"");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Format_WithBlankPath_Throws(string? path)
    {
        Action act = () => StartupCommand.Format(path!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryExtractPath_RoundTripsAQuotedPathWithSpaces()
    {
        string original = @"C:\dir with space\Scrybe.exe";
        string command = StartupCommand.Format(original);

        bool extracted = StartupCommand.TryExtractPath(command, out string path);

        extracted.Should().BeTrue();
        path.Should().Be(original);
    }

    [Fact]
    public void TryExtractPath_AcceptsABarePath()
    {
        bool extracted = StartupCommand.TryExtractPath(@"C:\tools\Scrybe.exe", out string path);

        extracted.Should().BeTrue();
        path.Should().Be(@"C:\tools\Scrybe.exe");
    }

    [Fact]
    public void TryExtractPath_TrimsSurroundingWhitespace()
    {
        bool extracted = StartupCommand.TryExtractPath("   C:\\tools\\Scrybe.exe   ", out string path);

        extracted.Should().BeTrue();
        path.Should().Be(@"C:\tools\Scrybe.exe");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryExtractPath_WithBlankInput_ReturnsFalse(string? command)
    {
        bool extracted = StartupCommand.TryExtractPath(command, out string path);

        extracted.Should().BeFalse();
        path.Should().BeEmpty();
    }
}
