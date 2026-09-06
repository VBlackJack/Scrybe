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
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scrybe.Core;
using Scrybe.Core.Logging;
using Scrybe.Core.Text;
using Scrybe.Ocr;

namespace Scrybe.App.Services;

/// <summary>Verifies the packaged runtime before normal startup, without user data or UI input.</summary>
internal static class PackageSelfTest
{
    internal const string Switch = "--self-test";
    private const string Expected = "ERROR connection refused 10.0.0.5:5432 path /etc/scrybe/agent.conf not found";
    private const double MaximumCer = 0.05;

    internal static async Task<int> RunAsync(string reportPath)
    {
        string path = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // CreateNew prevents an accidental overwrite of a previous report or unrelated file.
        await using FileStream report = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        FileLogger.Initialize(Path.Combine(Path.GetDirectoryName(path)!, "logs"));
        long started = Stopwatch.GetTimestamp();
        try
        {
            string root = AppContext.BaseDirectory;
            string[] required = ["Tesseract.dll", "x64/tesseract50.dll", "x64/leptonica-1.82.0.dll", "tessdata/eng.traineddata", "locales/en.json", "locales/fr.json", "selftest/console-sample.png"];
            foreach (string relative in required)
            {
                if (!File.Exists(Path.Combine(root, relative))) { throw new FileNotFoundException("Missing packaged asset.", relative); }
            }
            HashSet<string>? keys = null;
            foreach (string locale in new[] { "en", "fr" })
            {
                using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "locales", locale + ".json")));
                JsonProperty[] properties = document.RootElement.EnumerateObject().ToArray();
                HashSet<string> current = properties.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
                if (current.Count != properties.Length || (keys is not null && !keys.SetEquals(current)))
                { throw new InvalidDataException("Locale keys are duplicated or inconsistent."); }
                keys = current;
            }
            using TesseractOcrEngine engine = new(Path.Combine(root, AppConstants.TessdataDirName), AppConstants.DefaultOcrLanguage);
            byte[] image = await File.ReadAllBytesAsync(Path.Combine(root, "selftest", "console-sample.png"));
            string text = (await engine.RecognizeAsync(image)).Text;
            double cer = TextAccuracy.CharacterErrorRate(Expected, Regex.Replace(text.Trim(), @"\s+", " "));
            bool passed = cer <= MaximumCer;
            await JsonSerializer.SerializeAsync(report, new { passed, cer, maximumCer = MaximumCer, elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds, localeKeys = keys?.Count, runtime = Environment.Version.ToString(), assets = required });
            return passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            FileLogger.Error("Packaged self-test failed.", exception);
            await JsonSerializer.SerializeAsync(report, new { passed = false, error = exception.ToString() });
            return 1;
        }
        finally { FileLogger.Flush(); }
    }
}
