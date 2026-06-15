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

namespace Scrybe.App.Services;

/// <summary>One diagnostic label/value row before localization.</summary>
/// <param name="LabelKey">Localization key for the row label.</param>
/// <param name="Value">Display-ready diagnostic value.</param>
/// <param name="Exists">Optional runtime existence state for files and directories.</param>
public sealed record DiagnosticInfoRow(string LabelKey, string Value, bool? Exists = null);
