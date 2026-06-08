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

namespace Scrybe.Core.Snippets;

/// <summary>The result of rendering a snippet template: the resolved text and any unfilled parameters.</summary>
/// <param name="Text">The text with placeholders substituted (unfilled placeholders are left in place).</param>
/// <param name="MissingParameters">Names of parameters that had no value (injection must be refused).</param>
public sealed record TemplateRenderResult(string Text, IReadOnlyList<string> MissingParameters)
{
    /// <summary>Whether every placeholder was filled, so the text is safe to inject.</summary>
    public bool IsComplete => MissingParameters.Count == 0;
}
