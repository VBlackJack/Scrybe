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

using Scrybe.Core.Logging;

namespace Scrybe.Core.IO;

/// <summary>Writes files through a same-directory temporary file followed by an atomic rename.</summary>
public static class AtomicFileWriter
{
    /// <summary>
    /// Writes <paramref name="destinationPath"/> atomically by serializing into a temporary sibling file,
    /// flushing it, and then renaming it over the destination.
    /// </summary>
    /// <param name="destinationPath">Final path to write.</param>
    /// <param name="writeAsync">Delegate that writes the complete file content into the provided stream.</param>
    /// <param name="cancellationToken">Token used while writing and flushing the temporary file.</param>
    public static async Task WriteAsync(
        string destinationPath,
        Func<Stream, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(writeAsync);

        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = BuildTempPath(destinationPath);
        bool completed = false;

        try
        {
            await using (FileStream stream = new(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.WriteThrough | FileOptions.Asynchronous))
            {
                await writeAsync(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, destinationPath, overwrite: true);
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                DeleteTempFile(tempPath);
            }
        }
    }

    private static string BuildTempPath(string destinationPath)
    {
        string? directory = Path.GetDirectoryName(destinationPath);
        string fileName = Path.GetFileName(destinationPath);
        string tempFileName = $".{fileName}.{Guid.NewGuid():N}.tmp";
        return string.IsNullOrEmpty(directory)
            ? tempFileName
            : Path.Combine(directory, tempFileName);
    }

    private static void DeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            FileLogger.Warn(
                $"Failed to delete temporary file '{tempPath}' after an atomic write failure: "
                + $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}
