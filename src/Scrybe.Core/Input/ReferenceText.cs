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

using System.Text;

namespace Scrybe.Core.Input;

/// <summary>
/// Produces the deterministic reference string used to measure injection integrity against ground
/// truth (independent of OCR). Built by repeating <see cref="AppConstants.InjectionReferencePattern"/>.
/// </summary>
public static class ReferenceText
{
    /// <summary>Returns the reference string truncated to exactly <paramref name="length"/> characters.</summary>
    /// <param name="length">The desired length; values at or below zero yield an empty string.</param>
    public static string OfLength(int length)
    {
        if (length <= 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new(length + AppConstants.InjectionReferencePattern.Length);
        while (builder.Length < length)
        {
            builder.Append(AppConstants.InjectionReferencePattern);
        }

        return builder.ToString(0, length);
    }
}
