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

namespace Scrybe.Core.IO;

/// <summary>
/// Pure helper that resolves where captures are stored and builds timestamped file names.
/// Kept free of any UI or OS-capture dependency so it can be unit tested in isolation.
/// </summary>
public static class CapturePathResolver
{
    /// <summary>
    /// Resolves the directory in which captures should be written. When
    /// <paramref name="configuredDirectory"/> is <see langword="null"/> or blank, falls back
    /// to <c>%LOCALAPPDATA%/Scrybe/captures</c>.
    /// </summary>
    /// <param name="configuredDirectory">An explicit directory from settings, or <see langword="null"/>.</param>
    /// <returns>The resolved absolute directory path.</returns>
    public static string ResolveDirectory(string? configuredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return configuredDirectory;
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, AppConstants.AppName, AppConstants.CapturesSubDirName);
    }

    /// <summary>
    /// Builds a timestamped capture file name, for example <c>Scrybe_capture_20260607_151321_004.png</c>.
    /// </summary>
    /// <param name="timestamp">The moment the capture was taken.</param>
    /// <returns>The file name (without directory).</returns>
    public static string BuildFileName(DateTime timestamp)
    {
        string stamp = timestamp.ToString(AppConstants.CaptureFileTimestampFormat, CultureInfo.InvariantCulture);
        return $"{AppConstants.CaptureFileNamePrefix}{stamp}{AppConstants.CaptureFileExtension}";
    }
}
