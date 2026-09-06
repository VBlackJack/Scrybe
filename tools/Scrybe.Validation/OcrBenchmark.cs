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

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Scrybe.Core;
using Scrybe.Core.Models;
using Scrybe.Core.Text;
using Scrybe.Ocr;

namespace Scrybe.Validation;

internal sealed record OcrCase(string Id, string Text, double FontSize, double Dpi, string Foreground, string Background, double MaxNormalizedCer);

internal static class OcrBenchmark
{
    internal static byte[] Render(OcrCase sample)
    {
        const int Padding = 24;
        double scale = sample.Dpi / AppConstants.DipBaseline;
        FormattedText text = new(sample.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Consolas"), sample.FontSize, (Brush)new BrushConverter().ConvertFromString(sample.Foreground)!, scale);
        int width = (int)Math.Ceiling((text.WidthIncludingTrailingWhitespace + Padding * 2) * scale);
        int height = (int)Math.Ceiling((text.Height + Padding * 2) * scale);
        DrawingVisual visual = new();
        using (DrawingContext drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle((Brush)new BrushConverter().ConvertFromString(sample.Background)!, null, new Rect(0, 0, width / scale, height / scale));
            drawing.DrawText(text, new Point(Padding, Padding));
        }
        RenderTargetBitmap bitmap = new(width, height, sample.Dpi, sample.Dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using MemoryStream stream = new();
        encoder.Save(stream);
        return stream.ToArray();
    }

    internal static async Task RunAsync(string output, string corpusPath)
    {
        OcrCase[] corpus = JsonSerializer.Deserialize<OcrCase[]>(await File.ReadAllTextAsync(corpusPath), Program.Json)
            ?? throw new InvalidDataException("Missing OCR corpus.");
        if (corpus.Length == 0) { throw new InvalidDataException("Empty OCR corpus."); }
        List<object> results = [];
        bool passed = true;
        using TesseractOcrEngine engine = new(Path.Combine(AppContext.BaseDirectory, AppConstants.TessdataDirName), AppConstants.DefaultOcrLanguage);
        long coldStart = Stopwatch.GetTimestamp();
        engine.Prewarm();
        double coldMs = Stopwatch.GetElapsedTime(coldStart).TotalMilliseconds;
        for (int index = 0; index < corpus.Length; index++)
        {
            OcrCase sample = corpus[index];
            byte[] png = Render(sample);
            await File.WriteAllBytesAsync(Path.Combine(output, $"case-{index:D2}.png"), png);
            long start = Stopwatch.GetTimestamp();
            OcrResult recognized = await engine.RecognizeAsync(png);
            double ocrMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            foreach (OcrCleanupMode mode in Enum.GetValues<OcrCleanupMode>())
            {
                start = Stopwatch.GetTimestamp();
                string actual = TextPostProcessor.ProcessCapture(recognized.Text, mode).Text;
                double cleanupMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                string expected = TextPostProcessor.ProcessCapture(sample.Text, mode).Text;
                double normalizedCer = TextAccuracy.CharacterErrorRate(Normalize(expected), Normalize(actual));
                passed &= normalizedCer <= sample.MaxNormalizedCer;
                results.Add(new
                {
                    sample.Id,
                    sample.Dpi,
                    sample.FontSize,
                    mode = mode.ToString(),
                    ocrMs,
                    cleanupMs,
                    exactCer = TextAccuracy.CharacterErrorRate(expected, actual),
                    normalizedCer,
                    sample.MaxNormalizedCer,
                    expected,
                    actual
                });
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "ocr.json"), JsonSerializer.Serialize(new { passed, coldMs, corpusPath, results }, Program.Json));
        if (!passed) { throw new InvalidOperationException("OCR corpus exceeded its configured CER budget; see ocr.json."); }
    }

    private static string Normalize(string text) => Regex.Replace(text.Trim(), @"\s+", " ");
}
