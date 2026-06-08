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
using Scrybe.Core.IO;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for the pure <see cref="CapturePathResolver"/>.</summary>
public sealed class CapturePathResolverTests
{
    [Fact]
    public void ResolveDirectory_Null_FallsBackToLocalAppData()
    {
        string resolved = CapturePathResolver.ResolveDirectory(null);

        string expectedRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        resolved.Should().StartWith(expectedRoot);
        resolved.Should().EndWith(Path.Combine(AppConstants.AppName, AppConstants.CapturesSubDirName));
        Path.IsPathRooted(resolved).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveDirectory_BlankInput_FallsBackToDefault(string configured)
    {
        string resolved = CapturePathResolver.ResolveDirectory(configured);

        resolved.Should().EndWith(Path.Combine(AppConstants.AppName, AppConstants.CapturesSubDirName));
    }

    [Fact]
    public void ResolveDirectory_ExplicitDirectory_IsReturnedUnchanged()
    {
        string configured = Path.Combine("D:", "ScrybeCaptures");

        string resolved = CapturePathResolver.ResolveDirectory(configured);

        resolved.Should().Be(configured);
    }

    [Fact]
    public void BuildFileName_ProducesTimestampedPngName()
    {
        DateTime timestamp = new(2026, 6, 7, 15, 13, 21, 4);

        string fileName = CapturePathResolver.BuildFileName(timestamp);

        fileName.Should().Be("Scrybe_capture_20260607_151321_004.png");
    }
}
