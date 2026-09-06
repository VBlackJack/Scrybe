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

namespace Scrybe.Core.Models;

/// <summary>The outcome of a keystroke injection attempt.</summary>
/// <param name="Success">Whether the full sequence was injected.</param>
/// <param name="KeystrokesSent">Number of key events actually sent.</param>
/// <param name="UipiBlocked">Whether injection was blocked because the target is a higher integrity level.</param>
/// <param name="Aborted">Whether injection was cancelled by the emergency abort.</param>
public sealed record InjectionResult(bool Success, int KeystrokesSent, bool UipiBlocked, bool Aborted, InjectionFailureReason Reason = InjectionFailureReason.None);
