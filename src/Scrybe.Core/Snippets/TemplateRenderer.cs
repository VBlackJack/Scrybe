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

using System.Text.RegularExpressions;
using Scrybe.Core.Models;

namespace Scrybe.Core.Snippets;

/// <summary>
/// Pure snippet-template substitution. Replaces <c>{{param}}</c> placeholders with provided values,
/// reports any unfilled parameters (so a half-filled command is never injected), applies parameter
/// defaults, and supports an escape for literal double braces.
/// </summary>
public static class TemplateRenderer
{
    private static readonly Regex Placeholder = new(AppConstants.SnippetPlaceholderPattern, RegexOptions.Compiled);

    /// <summary>Renders a snippet, applying its parameter defaults before substituting provided values.</summary>
    /// <param name="snippet">The snippet to render.</param>
    /// <param name="providedValues">User-provided values keyed by parameter name.</param>
    public static TemplateRenderResult Render(Snippet snippet, IReadOnlyDictionary<string, string?> providedValues)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        ArgumentNullException.ThrowIfNull(providedValues);

        Dictionary<string, string?> effective = new(StringComparer.Ordinal);
        foreach (SnippetParameter parameter in snippet.Parameters)
        {
            string? value = providedValues.TryGetValue(parameter.Name, out string? provided) && !string.IsNullOrEmpty(provided)
                ? provided
                : parameter.Default;
            effective[parameter.Name] = value;
        }

        return Render(snippet.Template, effective);
    }

    /// <summary>Renders raw template text against the given values.</summary>
    /// <param name="template">The template text.</param>
    /// <param name="values">Values keyed by placeholder name.</param>
    public static TemplateRenderResult Render(string template, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);

        List<string> missing = [];

        string text = Placeholder.Replace(template, match =>
        {
            if (match.Value == AppConstants.SnippetEscapedOpen)
            {
                return AppConstants.SnippetLiteralOpen;
            }

            if (match.Value == AppConstants.SnippetEscapedClose)
            {
                return AppConstants.SnippetLiteralClose;
            }

            string name = match.Groups[1].Value;
            if (values.TryGetValue(name, out string? value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (!missing.Contains(name))
            {
                missing.Add(name);
            }

            return match.Value;
        });

        return new TemplateRenderResult(text, missing);
    }
}
