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

namespace Scrybe.Core.Services;

/// <summary>Publishes one accepted capture, with no side effects when review is cancelled.</summary>
public static class CapturePublication
{
    /// <summary>Preserves edited whitespace; a null review result cancels both clipboard and last-text publication.</summary>
    public static async Task<string?> PublishAsync(string text, IOcrTextStore textStore,
        IClipboardService clipboard, Func<string, Task<string?>>? review = null)
    {
        string? accepted = review is null ? text : await review(text).ConfigureAwait(false);
        if (accepted is null) { return null; }
        await clipboard.SetTextAsync(accepted).ConfigureAwait(false);
        textStore.Set(accepted);
        return accepted;
    }
}
