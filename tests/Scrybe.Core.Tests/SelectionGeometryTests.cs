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
using Scrybe.Core.Capture;
using Scrybe.Core.Models;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for the pure <see cref="SelectionGeometry"/> crop producer.</summary>
public sealed class SelectionGeometryTests
{
    private const int FrameWidth = 1000;
    private const int FrameHeight = 800;
    private const int MinSize = 8;

    [Fact]
    public void ToPhysicalCrop_DownRightDrag_AtUnityScale_ReturnsExpectedRect()
    {
        PixelRect? rect = SelectionGeometry.ToPhysicalCrop(10, 10, 110, 60, 1.0, FrameWidth, FrameHeight, MinSize);

        rect.Should().Be(new PixelRect(10, 10, 100, 50));
    }

    [Fact]
    public void ToPhysicalCrop_UpLeftDrag_NormalizesToSameRect()
    {
        PixelRect? rect = SelectionGeometry.ToPhysicalCrop(110, 60, 10, 10, 1.0, FrameWidth, FrameHeight, MinSize);

        rect.Should().Be(new PixelRect(10, 10, 100, 50));
    }

    [Fact]
    public void ToPhysicalCrop_At150Percent_ScalesDipToPhysicalPixels()
    {
        PixelRect? rect = SelectionGeometry.ToPhysicalCrop(10, 10, 110, 60, 1.5, FrameWidth, FrameHeight, MinSize);

        // 10*1.5=15, 110*1.5=165, 60*1.5=90 -> origin (15,15), size (150,75).
        rect.Should().Be(new PixelRect(15, 15, 150, 75));
    }

    [Fact]
    public void ToPhysicalCrop_BeyondBounds_IsClampedToFrame()
    {
        PixelRect? rect = SelectionGeometry.ToPhysicalCrop(900, 700, 1200, 1000, 1.0, FrameWidth, FrameHeight, MinSize);

        rect.Should().Be(new PixelRect(900, 700, 100, 100));
    }

    [Fact]
    public void ToPhysicalCrop_BelowMinimumSize_ReturnsNull()
    {
        PixelRect? rect = SelectionGeometry.ToPhysicalCrop(10, 10, 14, 14, 1.0, FrameWidth, FrameHeight, MinSize);

        rect.Should().BeNull();
    }

    [Fact]
    public void ToPhysicalCrop_ExactlyMinimumSize_IsValid()
    {
        PixelRect? rect = SelectionGeometry.ToPhysicalCrop(10, 10, 18, 18, 1.0, FrameWidth, FrameHeight, MinSize);

        rect.Should().Be(new PixelRect(10, 10, 8, 8));
    }
}
