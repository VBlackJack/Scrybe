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

using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;

namespace Scrybe.Core.Services;

/// <summary>
/// Clipboard service with a transient-failure retry policy over a platform <see cref="IClipboardWriter"/>.
/// The retry orchestration is platform-free so it can be unit tested with a fake writer. Never throws:
/// after exhausting retries it logs and returns.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    private readonly IClipboardWriter _writer;

    /// <summary>Initializes the service with the platform clipboard writer.</summary>
    /// <param name="writer">The low-level clipboard writer seam.</param>
    public ClipboardService(IClipboardWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
    }

    /// <inheritdoc />
    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= AppConstants.ClipboardRetryCount; attempt++)
        {
            try
            {
                _writer.SetText(text);
                return;
            }
            catch (Exception exception) when (attempt < AppConstants.ClipboardRetryCount)
            {
                FileLogger.Warn(
                    $"Clipboard write failed (attempt {attempt}/{AppConstants.ClipboardRetryCount}): {exception.Message}");
                await Task.Delay(AppConstants.ClipboardRetryDelayMs, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                FileLogger.Error("Clipboard write gave up after retries.", exception);
                return;
            }
        }
    }
}
