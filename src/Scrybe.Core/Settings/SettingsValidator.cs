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

using Scrybe.Core.Models;

namespace Scrybe.Core.Settings;

/// <summary>
/// Pure validation and clamping of <see cref="AppSettings"/>: out-of-range pacing is clamped to a sane
/// range, and an invalid enum (for example from a hand-edited file) falls back to its default. This
/// keeps a malformed-but-parseable file from producing nonsensical runtime behavior.
/// </summary>
public static class SettingsValidator
{
    /// <summary>Validates and clamps the settings in place, returning the same instance.</summary>
    /// <param name="settings">The settings to validate.</param>
    public static AppSettings Validate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.InjectionKeyDelayMs = Math.Clamp(
            settings.InjectionKeyDelayMs,
            AppConstants.InjectionKeyDelayMinMs,
            AppConstants.InjectionKeyDelayMaxMs);

        settings.InjectionEnterExtraDelayMs = Math.Clamp(
            settings.InjectionEnterExtraDelayMs,
            0,
            AppConstants.InjectionEnterExtraDelayMaxMs);

        if (!Enum.IsDefined(settings.CleanupMode))
        {
            settings.CleanupMode = OcrCleanupMode.Standard;
        }

        if (!Enum.IsDefined(settings.InjectionMode))
        {
            settings.InjectionMode = InjectionMode.Unicode;
        }

        if (string.IsNullOrWhiteSpace(settings.LocaleCode))
        {
            settings.LocaleCode = AppConstants.DefaultLocaleCode;
        }

        return settings;
    }
}
