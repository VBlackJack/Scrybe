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

namespace Scrybe.App.ViewModels;

/// <summary>One label/value row in the About window.</summary>
public sealed class AboutDetailRow
{
    /// <summary>Initializes an About detail row.</summary>
    public AboutDetailRow(string label, string value, bool isMissing = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Label = label;
        Value = value;
        IsMissing = isMissing;
    }

    /// <summary>Localized row label.</summary>
    public string Label { get; }

    /// <summary>Display-ready row value.</summary>
    public string Value { get; }

    /// <summary>Whether this row represents a missing runtime asset.</summary>
    public bool IsMissing { get; }
}
