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

using Scrybe.Core.Capture;
using Scrybe.Core.Input;
using Scrybe.Core.Models;
using Scrybe.Core.Text;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Guards special-key modifiers, capture whitespace and pointer-free geometry.</summary>
public sealed class CaptureAndScancodeRegressionTests
{
    [Theory]
    [InlineData(SpecialKey.Tab, AppConstants.TabScanCode)]
    [InlineData(SpecialKey.Enter, AppConstants.EnterScanCode)]
    public void ShiftedCharacterThenSpecial_ReleasesShiftBeforeSpecial(SpecialKey key, ushort code)
    {
        ScancodeEventBuilder builder = new();
        builder.Build(KeyStroke.FromCharacter('A'), 30, true);
        IReadOnlyList<ScancodeKeyEvent> events = builder.Build(KeyStroke.FromSpecial(key), 0, false);
        Assert.Equal(new ScancodeKeyEvent(AppConstants.LeftShiftScanCode, true), events[0]);
        Assert.Equal(new ScancodeKeyEvent(code, false), events[1]);
        Assert.Equal(new ScancodeKeyEvent(code, true), events[2]);
        Assert.False(builder.ShiftHeld);
        Assert.Equal(2, builder.Build(KeyStroke.FromCharacter('a'), 30, false).Count);
    }

    [Theory]
    [InlineData(OcrCleanupMode.Raw)]
    [InlineData(OcrCleanupMode.CodeFormatter)]
    public void CapturePipeline_PreservesFirstLineIndentationAndTrailingNewline(OcrCleanupMode mode)
    {
        const string Text = "    print(x)\n\tprint(y)  \n";
        Assert.Equal(Text, TextPostProcessor.ProcessCapture(Text, mode).Text);
    }

    [Fact]
    public void RawCapture_PreservesCrLfAndBoundaryWhitespace()
        => Assert.Equal(" \tfoo\r\n", TextPostProcessor.ProcessCapture(" \tfoo\r\n", OcrCleanupMode.Raw).Text);

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)]
    [InlineData(800, 600)]
    public void KeyboardSelection_CanStartMoveResizeAndRemainWithinMonitor(int width, int height)
    {
        PixelRect initial = KeyboardSelection.Create(width, height);
        PixelRect moved = KeyboardSelection.Adjust(initial, 10, -10, false, width, height);
        Assert.Equal(initial.X + 10, moved.X);
        Assert.Equal(initial.Y - 10, moved.Y);
        PixelRect resized = KeyboardSelection.Adjust(moved, 10, 10, true, width, height);
        Assert.Equal(moved.Width + 10, resized.Width);
        PixelRect maximum = KeyboardSelection.Adjust(resized, int.MaxValue, int.MaxValue, true, width, height);
        Assert.Equal(width, maximum.X + maximum.Width);
        Assert.Equal(height, maximum.Y + maximum.Height);
        PixelRect minimum = KeyboardSelection.Adjust(maximum, int.MinValue, int.MinValue, true, width, height);
        Assert.Equal(AppConstants.MinSelectionPhysicalPixels, minimum.Width);
        Assert.Equal(AppConstants.MinSelectionPhysicalPixels, minimum.Height);
        PixelRect origin = KeyboardSelection.Adjust(minimum, int.MinValue, int.MinValue, false, width, height);
        Assert.Equal(0, origin.X);
        Assert.Equal(0, origin.Y);
    }
}
