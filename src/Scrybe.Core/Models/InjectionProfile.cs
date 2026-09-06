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

/// <summary>Immutable pacing snapshot selected by the confirmed process name (without .exe).</summary>
public sealed record InjectionProfile(string ProcessName, InjectionMode Mode, int KeyDelayMs, int EnterExtraDelayMs)
{
    /// <summary>Rejects ambiguous process identifiers and unsupported pacing.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(ProcessName)
        && ProcessName == ProcessName.Trim()
        && ProcessName.IndexOfAny(['/', '\\', ':', '*', '?']) < 0
        && !ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        && Enum.IsDefined(Mode)
        && KeyDelayMs >= AppConstants.InjectionKeyDelayMinMs && KeyDelayMs <= AppConstants.InjectionKeyDelayMaxMs
        && EnterExtraDelayMs >= 0 && EnterExtraDelayMs <= AppConstants.InjectionEnterExtraDelayMaxMs;

    /// <summary>Resolves one exact process match; never guesses among duplicate profiles.</summary>
    public static InjectionProfile Resolve(AppSettings settings, string processName)
    {
        InjectionProfile[] matches = (settings.InjectionProfiles ?? [])
            .Where(profile => profile is not null && profile.IsValid
                && string.Equals(profile.ProcessName, processName, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : new(processName, settings.InjectionMode,
            Math.Clamp(settings.InjectionKeyDelayMs, AppConstants.InjectionKeyDelayMinMs, AppConstants.InjectionKeyDelayMaxMs),
            Math.Clamp(settings.InjectionEnterExtraDelayMs, 0, AppConstants.InjectionEnterExtraDelayMaxMs));
    }
}
