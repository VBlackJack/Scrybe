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
using Scrybe.Core.Logging;

namespace Scrybe.Core.IO;

/// <summary>Moves unreadable JSON aside before a store falls back to defaults or an empty collection.</summary>
public static class CorruptJsonQuarantine
{
    /// <summary>
    /// Best-effort quarantine for a corrupt JSON file. Returns the quarantine path when the move
    /// succeeds, otherwise <see langword="null"/>. Never throws.
    /// </summary>
    /// <param name="filePath">The corrupt JSON file to preserve.</param>
    /// <param name="storeName">Human-readable store name for the log entry.</param>
    public static string? TryMoveAside(string filePath, string storeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);

        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            string quarantinePath = BuildQuarantinePath(filePath);
            File.Move(filePath, quarantinePath);
            FileLogger.Warn($"Corrupt {storeName} JSON moved aside to '{quarantinePath}'.");
            return quarantinePath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            FileLogger.Warn(
                $"Failed to quarantine corrupt JSON file '{filePath}': "
                + $"{exception.GetType().Name}: {exception.Message}");
            return null;
        }
    }

    private static string BuildQuarantinePath(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        string fileName = Path.GetFileName(filePath);
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        string quarantineName = $"{fileName}.corrupt.{stamp}.json";
        return string.IsNullOrEmpty(directory)
            ? quarantineName
            : Path.Combine(directory, quarantineName);
    }
}
