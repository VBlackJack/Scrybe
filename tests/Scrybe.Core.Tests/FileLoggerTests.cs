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

using System.Globalization;
using FluentAssertions;
using Scrybe.Core;
using Scrybe.Core.Logging;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>
/// Tests for <see cref="FileLogger"/>. Because the logger is a process-wide static, each
/// test initializes it against its own unique temporary directory so the cases stay isolated.
/// </summary>
public sealed class FileLoggerTests
{
    /// <summary>Builds the dated log file name the logger is expected to produce today.</summary>
    private static string ExpectedLogFileName()
        => $"{AppConstants.AppName}_{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}{AppConstants.LogFileExtension}";

    private static string CreateUniqueTempDirectory()
        => Path.Combine(Path.GetTempPath(), "ScrybeLoggerTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Info_WritesDatedFile_WithLevelAndMessage()
    {
        string directory = CreateUniqueTempDirectory();
        const string message = "skeleton bootstrap entry";

        try
        {
            FileLogger.SetEnabled(true);
            FileLogger.Initialize(directory);
            FileLogger.Info(message);
            FileLogger.Flush();

            string expectedPath = Path.Combine(directory, ExpectedLogFileName());
            File.Exists(expectedPath).Should().BeTrue("an Info entry must create the dated log file");

            string content = File.ReadAllText(expectedPath);
            content.Should().Contain("[INFO]", "entries are formatted with an uppercase level tag");
            content.Should().Contain(message, "the original message must be preserved");
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [Fact]
    public void SetEnabled_False_DiscardsEntries()
    {
        string directory = CreateUniqueTempDirectory();

        try
        {
            FileLogger.Initialize(directory);
            FileLogger.SetEnabled(false);
            FileLogger.Info("this entry must be discarded");
            FileLogger.Flush();

            string expectedPath = Path.Combine(directory, ExpectedLogFileName());
            File.Exists(expectedPath).Should().BeFalse("a disabled logger must not write anything to disk");
        }
        finally
        {
            FileLogger.SetEnabled(true);
            TryDelete(directory);
        }
    }

    [Fact]
    public void Logging_NeverThrows_WhenDirectoryIsInvalid()
    {
        // A path containing a null character can never be created or written to.
        string invalidDirectory = "Z:\\scrybe-invalid\0directory";

        Action act = () =>
        {
            FileLogger.SetEnabled(true);
            FileLogger.Initialize(invalidDirectory);
            FileLogger.Info("entry into an unwritable location");
            FileLogger.Error("failure", new InvalidOperationException("boom"));
            FileLogger.Flush();
        };

        act.Should().NotThrow("logging failures must be swallowed and never crash the caller");
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; a leftover temp directory must not fail the test run.
        }
    }
}
