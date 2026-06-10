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

namespace Scrybe.Core.Startup;

/// <summary>
/// Pure helpers to format and parse the autostart command string stored under the per-user Run key.
/// Formatting quotes the path so it survives spaces; parsing accepts both a quoted and a bare path so
/// an entry written by another tool, or by an older version, is still understood.
/// </summary>
public static class StartupCommand
{
    private const char Quote = '"';

    /// <summary>Wraps the executable path in double quotes so the autostart command survives spaces.</summary>
    /// <param name="executablePath">Absolute path to the executable to launch at logon.</param>
    /// <returns>The quoted command string.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is null, empty, or whitespace.</exception>
    public static string Format(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("The executable path must be a non-empty value.", nameof(executablePath));
        }

        return string.Concat(Quote, executablePath.Trim(), Quote);
    }

    /// <summary>
    /// Extracts the executable path from a registered command string. Accepts a quoted path or a bare
    /// unquoted path and trims surrounding whitespace.
    /// </summary>
    /// <param name="command">The raw registry command string.</param>
    /// <param name="path">The parsed executable path when this returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a non-empty path was extracted; otherwise <see langword="false"/>.</returns>
    public static bool TryExtractPath(string? command, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        string trimmed = command.Trim();
        if (trimmed[0] == Quote)
        {
            int closingQuote = trimmed.IndexOf(Quote, 1);
            if (closingQuote <= 1)
            {
                return false;
            }

            string quoted = trimmed[1..closingQuote].Trim();
            if (quoted.Length == 0)
            {
                return false;
            }

            path = quoted;
            return true;
        }

        path = trimmed;
        return true;
    }
}
