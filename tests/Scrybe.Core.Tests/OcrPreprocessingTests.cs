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
using Scrybe.Core.Imaging;
using Scrybe.Core.Models;
using Scrybe.Core.Tests.TestSupport;
using Scrybe.Ocr;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>
/// Empirical before/after test: the light-on-dark console fixture that reproduces the 3a failure must
/// be recognized correctly once the pre-processing pipeline inverts polarity, and measurably better
/// than feeding the raw light-on-dark image to the engine.
/// </summary>
public sealed class OcrPreprocessingTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>Initializes the test with the xUnit output sink used to report the measured CER gain.</summary>
    /// <param name="output">The xUnit test output helper.</param>
    public OcrPreprocessingTests(ITestOutputHelper output) => _output = output;

    private const string ExpectedText =
        "Active: failed (Result: exit-code) "
        + "Process: 4821 ExecStart=/usr/bin/scrybe-agent (code=exited) "
        + "Error: bind 0.0.0.0:8443 permission denied";

    private const double MaxCharacterErrorRateWithPreprocessing = 0.01;

    [Fact]
    public async Task Preprocessing_FixesLightOnDarkTokens_AndBeatsRawOcr()
    {
        string tessdataPath = Path.Combine(AppContext.BaseDirectory, AppConstants.TessdataDirName);
        byte[] darkFixture = await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "console-dark.png"));

        using TesseractOcrEngine engine = new(tessdataPath, AppConstants.DefaultOcrLanguage);

        OcrResult rawResult = await engine.RecognizeAsync(darkFixture);
        string rawText = TextMetrics.Normalize(rawResult.Text);
        double cerWithout = TextMetrics.CharacterErrorRate(ExpectedText, rawText);

        PixelBuffer decoded = TestImaging.Decode(darkFixture);
        PreprocessingResult preprocessed = ImagePreprocessor.Process(decoded, PreprocessingOptions.Default);
        preprocessed.Inverted.Should().BeTrue("the light-on-dark fixture must be inverted");

        byte[] processedImage = TestImaging.Encode(preprocessed.Output);
        OcrResult processedResult = await engine.RecognizeAsync(processedImage);
        string processedText = TextMetrics.Normalize(processedResult.Text);
        double cerWith = TextMetrics.CharacterErrorRate(ExpectedText, processedText);

        _output.WriteLine($"CER without pre-processing: {cerWithout:P2}  ->  '{rawText}'");
        _output.WriteLine($"CER with    pre-processing: {cerWith:P2}  ->  '{processedText}'");

        cerWith.Should().BeLessThanOrEqualTo(
            MaxCharacterErrorRateWithPreprocessing,
            "with pre-processing the clean fixture should be near-perfect (with='{0}', without='{1}', cerWithout={2:P1})",
            processedText, rawText, cerWithout);

        cerWith.Should().BeLessThan(
            cerWithout,
            "pre-processing must measurably improve light-on-dark OCR (cerWith={0:P1}, cerWithout={1:P1}; with='{2}'; without='{3}')",
            cerWith, cerWithout, processedText, rawText);

        processedText.Should().Contain("0.0.0.0", "the leading zero previously mangled to '@' must be correct");
        processedText.Should().Contain("code=exited", "the '=' previously read as '-' must be correct");
    }
}
