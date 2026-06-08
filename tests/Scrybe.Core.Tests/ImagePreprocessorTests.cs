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
using Scrybe.Core.Imaging;
using Scrybe.Core.Models;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Pure-transform tests for <see cref="ImagePreprocessor"/>: polarity, upscale, determinism.</summary>
public sealed class ImagePreprocessorTests
{
    private const byte DarkGray = 40;
    private const byte LightGray = 240;

    [Fact]
    public void Process_LightOnDark_DetectsAndInverts()
    {
        PixelBuffer lightOnDark = MakeBars(background: DarkGray, ink: LightGray, barHeight: 12);

        PreprocessingResult result = ImagePreprocessor.Process(lightOnDark, PreprocessingOptions.Default);

        result.Inverted.Should().BeTrue("a dark background with light text must be inverted for OCR");
    }

    [Fact]
    public void Process_DarkOnLight_DoesNotInvert()
    {
        PixelBuffer darkOnLight = MakeBars(background: LightGray, ink: DarkGray, barHeight: 12);

        PreprocessingResult result = ImagePreprocessor.Process(darkOnLight, PreprocessingOptions.Default);

        result.Inverted.Should().BeFalse("a light background with dark text is already in the expected polarity");
    }

    [Fact]
    public void Process_SmallText_TriggersUpscale()
    {
        PixelBuffer smallText = MakeBars(background: LightGray, ink: DarkGray, barHeight: 8);

        PreprocessingResult result = ImagePreprocessor.Process(smallText, PreprocessingOptions.Default);

        result.AppliedUpscaleFactor.Should().Be(AppConstants.PreprocessUpscaleFactor);
        result.Output.Width.Should().Be(smallText.Width * AppConstants.PreprocessUpscaleFactor);
    }

    [Fact]
    public void Process_LargeText_DoesNotUpscale()
    {
        PixelBuffer largeText = MakeBars(background: LightGray, ink: DarkGray, barHeight: 40);

        PreprocessingResult result = ImagePreprocessor.Process(largeText, PreprocessingOptions.Default);

        result.AppliedUpscaleFactor.Should().Be(1);
        result.Output.Width.Should().Be(largeText.Width);
    }

    [Fact]
    public void Process_IsDeterministic()
    {
        PixelBuffer input = MakeBars(background: DarkGray, ink: LightGray, barHeight: 12);

        PreprocessingResult first = ImagePreprocessor.Process(input, PreprocessingOptions.Default);
        PreprocessingResult second = ImagePreprocessor.Process(input, PreprocessingOptions.Default);

        second.Output.Bgra.Should().Equal(first.Output.Bgra);
        second.Inverted.Should().Be(first.Inverted);
        second.AppliedUpscaleFactor.Should().Be(first.AppliedUpscaleFactor);
    }

    [Fact]
    public void Process_WithBinarizationEnabled_OutputsBlackOrWhite()
    {
        PixelBuffer input = MakeBars(background: LightGray, ink: DarkGray, barHeight: 40);
        PreprocessingOptions options = PreprocessingOptions.Default with { ApplyBinarization = true };

        PreprocessingResult result = ImagePreprocessor.Process(input, options);

        for (int i = 0; i < result.Output.Bgra.Length; i += 4)
        {
            byte channel = result.Output.Bgra[i];
            channel.Should().Match(value => value == 0 || value == 255, "binarization must yield pure black or white");
        }
    }

    /// <summary>Builds a 200×120 buffer with full-width ink bars over a margin, leaving a clean border.</summary>
    private static PixelBuffer MakeBars(byte background, byte ink, int barHeight)
    {
        const int width = 200;
        const int height = 120;
        const int margin = 6;
        const int spacing = 12;
        const int bytesPerPixel = 4;

        byte[] bgra = new byte[width * height * bytesPerPixel];
        for (int i = 0; i < width * height; i++)
        {
            int offset = i * bytesPerPixel;
            bgra[offset] = background;
            bgra[offset + 1] = background;
            bgra[offset + 2] = background;
            bgra[offset + 3] = 255;
        }

        int y = spacing;
        while (y + barHeight <= height - spacing)
        {
            for (int barY = y; barY < y + barHeight; barY++)
            {
                for (int x = margin; x < width - margin; x++)
                {
                    int offset = ((barY * width) + x) * bytesPerPixel;
                    bgra[offset] = ink;
                    bgra[offset + 1] = ink;
                    bgra[offset + 2] = ink;
                }
            }

            y += barHeight + spacing;
        }

        return new PixelBuffer(bgra, width, height);
    }
}
