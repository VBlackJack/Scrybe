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
using System.Text;
using System.Text.RegularExpressions;
using Scrybe.Core.Models;

namespace Scrybe.Core.Text;

/// <summary>
/// Pure, UI-free post-processor for OCR text. Three conservative, individually-toggleable stages:
/// line-wrap repair, shell-prompt stripping, and CONTEXT-AWARE confusion correction. Confusion fixes
/// are applied only inside tokens that both match a technical shape (IP, GUID, hex, numeric) and whose
/// corrected form validates - so an email's <c>@</c>, the letter <c>O</c> in a word, and ordinary prose
/// are never touched.
/// </summary>
public static class TextPostProcessor
{
    private const int OctetMaxValue = 255;
    private const int PortMaxValue = 65535;
    private const char LineFeed = '\n';

    private static readonly Regex TokenPattern = new(@"\S+", RegexOptions.Compiled);
    private static readonly Regex KeyValueStart = new(AppConstants.OcrKeyValueStartPattern, RegexOptions.Compiled);
    private static readonly Regex BulletStart = new(AppConstants.OcrBulletStartPattern, RegexOptions.Compiled);
    private static readonly Regex IpToken = new(AppConstants.OcrIpTokenPattern, RegexOptions.Compiled);
    private static readonly Regex GuidToken = new(AppConstants.OcrGuidTokenPattern, RegexOptions.Compiled);
    private static readonly Regex HexToken = new(AppConstants.OcrHexTokenPattern, RegexOptions.Compiled);
    private static readonly Regex NumericToken = new(AppConstants.OcrNumericTokenPattern, RegexOptions.Compiled);

    private static readonly Regex[] PromptPatterns =
        [.. AppConstants.OcrPromptPatterns.Select(pattern => new Regex(pattern, RegexOptions.Compiled))];

    private static readonly Regex[] TimestampPatterns =
        [.. AppConstants.OcrTimestampPatterns.Select(pattern => new Regex(pattern, RegexOptions.Compiled))];

    private static readonly Regex LogLevel = new(AppConstants.OcrLogLevelPattern, RegexOptions.Compiled);
    private static readonly Regex PidPrefix = new(AppConstants.OcrPidPattern, RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<char, char> IpConfusions = new Dictionary<char, char>
    {
        ['@'] = '0',
        ['O'] = '0',
        ['o'] = '0',
        ['I'] = '1',
        ['l'] = '1',
    };

    private static readonly IReadOnlyDictionary<char, char> HexConfusions = new Dictionary<char, char>
    {
        ['O'] = '0',
        ['o'] = '0',
        ['I'] = '1',
        ['l'] = '1',
        ['S'] = '5',
        ['Z'] = '2',
    };

    private static readonly IReadOnlyDictionary<char, char> NumericConfusions = new Dictionary<char, char>
    {
        ['O'] = '0',
        ['o'] = '0',
        ['I'] = '1',
        ['l'] = '1',
        ['S'] = '5',
        ['Z'] = '2',
        ['B'] = '8',
    };

    /// <summary>Runs the enabled post-processing stages over <paramref name="input"/>.</summary>
    /// <param name="input">The raw OCR text.</param>
    /// <param name="options">Which stages to run.</param>
    public static TextPostProcessingResult Process(string input, TextPostProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);

        // Raw / passthrough: return the input byte-for-byte (the safety net must never alter text).
        if (!options.RepairWraps && !options.StripPrompts && !options.FixConfusions && !options.StripLogDecorations)
        {
            return new TextPostProcessingResult(input, 0, 0, 0, 0);
        }

        List<string> lines = [.. input.Replace("\r\n", "\n").Split(LineFeed)];
        int mergedLines = 0;
        int strippedPrompts = 0;
        int correctedTokens = 0;
        int strippedLogDecorations = 0;

        if (options.RepairWraps)
        {
            lines = RepairWraps(lines, out mergedLines);
        }

        if (options.StripPrompts)
        {
            StripPrompts(lines, ref strippedPrompts);
        }

        if (options.StripLogDecorations)
        {
            StripLogDecorations(lines, ref strippedLogDecorations);
        }

        if (options.FixConfusions)
        {
            FixConfusions(lines, ref correctedTokens);
        }

        string text = string.Join(LineFeed, lines);
        return new TextPostProcessingResult(text, mergedLines, strippedPrompts, correctedTokens, strippedLogDecorations);
    }

    /// <summary>Applies the capture mode, retaining boundary whitespace in Raw and Code Formatter.</summary>
    /// <param name="input">Verbatim engine output.</param>
    /// <param name="mode">User-selected capture mode.</param>
    public static TextPostProcessingResult ProcessCapture(string input, OcrCleanupMode mode)
    {
        ArgumentNullException.ThrowIfNull(input);
        bool preserveWhitespace = mode is OcrCleanupMode.Raw or OcrCleanupMode.CodeFormatter;
        TextPostProcessingResult result = Process(preserveWhitespace ? input : input.Trim(), TextPostProcessingOptions.ForMode(mode));
        return preserveWhitespace ? result : result with { Text = result.Text.Trim() };
    }
    private static List<string> RepairWraps(List<string> lines, out int mergedLines)
    {
        mergedLines = 0;
        List<string> result = [];

        foreach (string line in lines)
        {
            if (result.Count > 0 && IsContinuation(line))
            {
                result[^1] = result[^1].TrimEnd() + " " + line.TrimStart();
                mergedLines++;
            }
            else
            {
                result.Add(line);
            }
        }

        return result;
    }

    private static bool IsContinuation(string line)
    {
        string trimmed = line.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (StartsNewLogicalLine(trimmed))
        {
            return false;
        }

        // Continuations resume mid-sentence, so they start with a lowercase letter.
        return char.IsLower(trimmed[0]);
    }

    private static bool StartsNewLogicalLine(string trimmed)
        => IsPrompt(trimmed)
            || KeyValueStart.IsMatch(trimmed)
            || BulletStart.IsMatch(trimmed)
            || trimmed[0] == '[';

    private static bool IsPrompt(string line)
    {
        foreach (Regex prompt in PromptPatterns)
        {
            if (prompt.IsMatch(line))
            {
                return true;
            }
        }

        return false;
    }

    private static void StripPrompts(List<string> lines, ref int strippedPrompts)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            foreach (Regex prompt in PromptPatterns)
            {
                Match match = prompt.Match(lines[i]);
                if (match.Success)
                {
                    lines[i] = lines[i][match.Length..];
                    strippedPrompts++;
                    break;
                }
            }
        }
    }

    private static void StripLogDecorations(List<string> lines, ref int strippedLogDecorations)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            string original = line;

            foreach (Regex timestamp in TimestampPatterns)
            {
                Match match = timestamp.Match(line);
                if (match.Success)
                {
                    line = line[match.Length..];
                    break;
                }
            }

            Match level = LogLevel.Match(line);
            if (level.Success)
            {
                line = line[level.Length..];
            }

            line = PidPrefix.Replace(line, "$1$2");

            if (!string.Equals(line, original, StringComparison.Ordinal))
            {
                lines[i] = line;
                strippedLogDecorations++;
            }
        }
    }

    private static void FixConfusions(List<string> lines, ref int correctedTokens)
    {
        int[] counter = [0];
        for (int i = 0; i < lines.Count; i++)
        {
            lines[i] = TokenPattern.Replace(lines[i], match => CorrectToken(match.Value, counter));
        }

        correctedTokens = counter[0];
    }

    private static string CorrectToken(string token, int[] counter)
    {
        string? corrected = TryFixIp(token)
            ?? TryFixGuid(token)
            ?? TryFixHex(token)
            ?? TryFixNumeric(token);

        if (corrected is not null && !string.Equals(corrected, token, StringComparison.Ordinal))
        {
            counter[0]++;
            return corrected;
        }

        return token;
    }

    private static string? TryFixIp(string token)
    {
        if (!IpToken.IsMatch(token))
        {
            return null;
        }

        string mapped = MapConfusions(token, IpConfusions);
        return IsValidIpToken(mapped) ? mapped : null;
    }

    private static string? TryFixGuid(string token)
    {
        if (!GuidToken.IsMatch(token))
        {
            return null;
        }

        string mapped = MapConfusions(token, HexConfusions);
        return Guid.TryParse(mapped, out _) ? mapped : null;
    }

    private static string? TryFixHex(string token)
    {
        if (!HexToken.IsMatch(token))
        {
            return null;
        }

        string mapped = MapConfusions(token, HexConfusions);
        return IsAllHex(mapped) ? mapped : null;
    }

    private static string? TryFixNumeric(string token)
    {
        if (!NumericToken.IsMatch(token))
        {
            return null;
        }

        int digitCount = token.Count(char.IsDigit);
        int letterCount = token.Length - digitCount;

        // Only correct tokens that are predominantly digits, so labels like "B5" are left alone.
        if (letterCount == 0 || digitCount <= letterCount)
        {
            return null;
        }

        string mapped = MapConfusions(token, NumericConfusions);
        return mapped.All(char.IsDigit) ? mapped : null;
    }

    private static string MapConfusions(string token, IReadOnlyDictionary<char, char> confusions)
    {
        StringBuilder builder = new(token.Length);
        foreach (char character in token)
        {
            builder.Append(confusions.TryGetValue(character, out char replacement) ? replacement : character);
        }

        return builder.ToString();
    }

    private static bool IsValidIpToken(string token)
    {
        string address = token;
        int colonIndex = token.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex >= 0)
        {
            string port = token[(colonIndex + 1)..];
            if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int portValue)
                || portValue > PortMaxValue)
            {
                return false;
            }

            address = token[..colonIndex];
        }

        string[] octets = address.Split('.');
        if (octets.Length != 4)
        {
            return false;
        }

        foreach (string octet in octets)
        {
            if (!int.TryParse(octet, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                || value > OctetMaxValue)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllHex(string token)
    {
        foreach (char character in token)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return token.Length > 0;
    }
}
