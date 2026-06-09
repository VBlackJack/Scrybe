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
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Scrybe.Core.Settings;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for settings persistence: round-trip, resilience, tolerance, clamping and enum validation.</summary>
public sealed class SettingsStoreTests
{
    [Fact]
    public async Task RoundTrip_PreservesAllValues()
    {
        string path = CreateTempPath();
        try
        {
            AppSettings saved = new()
            {
                EnableLogging = false,
                LocaleCode = "fr",
                CleanupMode = OcrCleanupMode.LogCleaner,
                InjectionMode = InjectionMode.Scancode,
                InjectionKeyDelayMs = 40,
                DebugInjectionEnabled = true,
                ClipboardInjectHotkeyModifiers = "Control+Shift",
                ClipboardInjectHotkeyKey = "B",
                ClearClipboardAfterInjection = false,
                PrewarmOnStartup = false,
                CapturesDirectory = @"D:\caps",
            };
            ISettingsStore store = new JsonSettingsStore(path);

            await store.SaveAsync(saved);
            SettingsLoadResult result = await store.LoadAsync();

            result.Existed.Should().BeTrue();
            result.Settings.Should().BeEquivalentTo(saved);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task MissingFile_ReturnsDefaults()
    {
        ISettingsStore store = new JsonSettingsStore(CreateTempPath());

        SettingsLoadResult result = await store.LoadAsync();

        result.Existed.Should().BeFalse();
        result.Settings.CleanupMode.Should().Be(OcrCleanupMode.Standard);
        result.Settings.EnableLogging.Should().BeTrue();
        result.Settings.ClipboardInjectHotkeyModifiers.Should().Be(AppConstants.DefaultClipboardInjectHotkeyModifiers);
        result.Settings.ClipboardInjectHotkeyKey.Should().Be(AppConstants.DefaultClipboardInjectHotkeyKey);
        result.Settings.ClearClipboardAfterInjection.Should().BeTrue();
        result.Settings.DebugInjectionEnabled.Should().BeFalse();
    }

    [Fact]
    public void Defaults_DisableDebugInjectionHotkeys()
    {
        AppSettings settings = new();

        settings.DebugInjectionEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DefaultRoundTrip_DisablesDebugInjectionHotkeys()
    {
        string path = CreateTempPath();
        try
        {
            ISettingsStore store = new JsonSettingsStore(path);

            await store.SaveAsync(new AppSettings());
            SettingsLoadResult result = await store.LoadAsync();

            result.Settings.DebugInjectionEnabled.Should().BeFalse();
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task CorruptFile_ReturnsDefaultsWithoutThrowing()
    {
        string path = CreateTempPath();
        await File.WriteAllTextAsync(path, "}{ not json");
        try
        {
            ISettingsStore store = new JsonSettingsStore(path);

            SettingsLoadResult result = await store.LoadAsync();

            result.Settings.CleanupMode.Should().Be(OcrCleanupMode.Standard);
            result.Settings.InjectionKeyDelayMs.Should().Be(AppConstants.InjectionKeyDelayMs);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task MissingFields_KeepDefaults()
    {
        string path = CreateTempPath();
        await File.WriteAllTextAsync(path, "{ \"EnableLogging\": false }");
        try
        {
            SettingsLoadResult result = await new JsonSettingsStore(path).LoadAsync();

            result.Settings.EnableLogging.Should().BeFalse();
            result.Settings.CleanupMode.Should().Be(OcrCleanupMode.Standard);
            result.Settings.InjectionKeyDelayMs.Should().Be(AppConstants.InjectionKeyDelayMs);
            result.Settings.ClearClipboardAfterInjection.Should().BeTrue();
            result.Settings.DebugInjectionEnabled.Should().BeFalse();
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task OutOfRangePacing_IsClamped()
    {
        string path = CreateTempPath();
        await File.WriteAllTextAsync(path, "{ \"InjectionKeyDelayMs\": 99999 }");
        try
        {
            SettingsLoadResult result = await new JsonSettingsStore(path).LoadAsync();

            result.Settings.InjectionKeyDelayMs.Should().Be(AppConstants.InjectionKeyDelayMaxMs);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void DebugPacingPresets_AreWithinValidatedRange()
    {
        AppConstants.DebugInjectionPacingPresetsMs.Should().NotBeEmpty();
        AppConstants.DebugInjectionPacingPresetsMs.Should().OnlyContain(
            preset => preset >= AppConstants.InjectionKeyDelayMinMs
            && preset <= AppConstants.InjectionKeyDelayMaxMs);
    }

    [Fact]
    public async Task InvalidEnum_FallsBackToDefault()
    {
        string path = CreateTempPath();
        await File.WriteAllTextAsync(path, "{ \"CleanupMode\": 99, \"InjectionMode\": 77 }");
        try
        {
            SettingsLoadResult result = await new JsonSettingsStore(path).LoadAsync();

            result.Settings.CleanupMode.Should().Be(OcrCleanupMode.Standard);
            result.Settings.InjectionMode.Should().Be(InjectionMode.Unicode);
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static string CreateTempPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ScrybeSettingsTests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
