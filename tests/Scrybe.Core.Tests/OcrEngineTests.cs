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
using Scrybe.Core.Models;
using Scrybe.Core.Tests.TestSupport;
using Scrybe.Ocr;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>
/// Real OCR test: feeds a bundled clean monospaced (dark-on-light) console fixture to the Tesseract
/// engine and asserts the character error rate stays low.
/// </summary>
public sealed class OcrEngineTests
{
    private const string ExpectedText = "ERROR connection refused 10.0.0.5:5432 path /etc/scrybe/agent.conf not found";
    private const double MaxCharacterErrorRate = 0.05;

    [Fact]
    public async Task RecognizeAsync_CleanConsoleFixture_HasLowCharacterErrorRate()
    {
        string tessdataPath = Path.Combine(AppContext.BaseDirectory, AppConstants.TessdataDirName);
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "console-sample.png");
        byte[] imageBytes = await File.ReadAllBytesAsync(fixturePath);

        using TesseractOcrEngine engine = new(tessdataPath, AppConstants.DefaultOcrLanguage);
        OcrResult result = await engine.RecognizeAsync(imageBytes);

        string actual = TextMetrics.Normalize(result.Text);
        double cer = TextMetrics.CharacterErrorRate(ExpectedText, actual);

        cer.Should().BeLessThanOrEqualTo(
            MaxCharacterErrorRate,
            "the clean fixture should be recognized almost perfectly (got: '{0}')", actual);
    }
}
