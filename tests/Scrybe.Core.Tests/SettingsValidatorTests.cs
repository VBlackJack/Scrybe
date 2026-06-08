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

using FluentAssertions;
using Scrybe.Core;
using Scrybe.Core.Models;
using Scrybe.Core.Settings;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for pure settings validation rules.</summary>
public sealed class SettingsValidatorTests
{
    [Fact]
    public void Validate_CaptureHistoryMaxEntriesBelowMinimum_IsClamped()
    {
        AppSettings settings = new()
        {
            CaptureHistoryMaxEntries = AppConstants.CaptureHistoryMaxEntriesMin - 1,
        };

        SettingsValidator.Validate(settings);

        settings.CaptureHistoryMaxEntries.Should().Be(AppConstants.CaptureHistoryMaxEntriesMin);
    }

    [Fact]
    public void Validate_CaptureHistoryMaxEntriesAboveMaximum_IsClamped()
    {
        AppSettings settings = new()
        {
            CaptureHistoryMaxEntries = AppConstants.CaptureHistoryMaxEntriesMax + 1,
        };

        SettingsValidator.Validate(settings);

        settings.CaptureHistoryMaxEntries.Should().Be(AppConstants.CaptureHistoryMaxEntriesMax);
    }

    [Fact]
    public void Validate_CaptureHistoryMaxEntriesInRange_PassesThrough()
    {
        AppSettings settings = new()
        {
            CaptureHistoryMaxEntries = AppConstants.DefaultCaptureHistoryMaxEntries + 1,
        };

        SettingsValidator.Validate(settings);

        settings.CaptureHistoryMaxEntries.Should().Be(AppConstants.DefaultCaptureHistoryMaxEntries + 1);
    }
}
