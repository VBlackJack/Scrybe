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

namespace Scrybe.Core.Text;

/// <summary>Character error rate using edit distance; whitespace policy belongs to the caller.</summary>
public static class TextAccuracy
{
    /// <summary>Returns edits divided by expected characters (may exceed one for insertions).</summary>
    public static double CharacterErrorRate(string expected, string actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        int[] previous = Enumerable.Range(0, actual.Length + 1).ToArray();
        int[] current = new int[actual.Length + 1];
        for (int row = 1; row <= expected.Length; row++)
        {
            current[0] = row;
            for (int column = 1; column <= actual.Length; column++)
            {
                current[column] = Math.Min(Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + (expected[row - 1] == actual[column - 1] ? 0 : 1));
            }
            (previous, current) = (current, previous);
        }
        return (double)previous[actual.Length] / Math.Max(1, expected.Length);
    }
}
