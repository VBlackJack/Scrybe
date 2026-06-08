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

/// <summary>Thread-safe in-memory store for the most recently recognized text.</summary>
public sealed class OcrTextStore : IOcrTextStore
{
    private readonly Lock _gate = new();
    private string? _lastText;

    /// <inheritdoc />
    public string? LastText
    {
        get
        {
            lock (_gate)
            {
                return _lastText;
            }
        }
    }

    /// <inheritdoc />
    public void Set(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (_gate)
        {
            _lastText = text;
        }
    }
}
