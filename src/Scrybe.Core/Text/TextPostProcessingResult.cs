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

/// <summary>The cleaned text plus per-stage counts, for logging and tests.</summary>
/// <param name="Text">The post-processed text.</param>
/// <param name="MergedLines">Number of lines merged into their predecessor by wrap repair.</param>
/// <param name="StrippedPrompts">Number of leading shell prompts removed.</param>
/// <param name="CorrectedTokens">Number of technical tokens whose OCR confusions were corrected.</param>
/// <param name="StrippedLogDecorations">Number of lines whose leading log decorations were stripped.</param>
public sealed record TextPostProcessingResult(
    string Text,
    int MergedLines,
    int StrippedPrompts,
    int CorrectedTokens,
    int StrippedLogDecorations);
