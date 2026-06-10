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
using Scrybe.App.Services;
using Scrybe.App.ViewModels;
using Scrybe.Core.Input;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>
/// Tests the start-with-Windows invariant of the settings save flow: the registry is touched only in
/// the intended direction, driven by the checkbox against the current registry state.
/// </summary>
public sealed class SettingsViewModelStartupTests
{
    [Fact]
    public async Task Save_StartWithWindowsCheckedWhileDisabled_EnablesOnceAndNeverDisables()
    {
        RecordingStartupRegistration startup = new(initiallyEnabled: false);
        SettingsViewModel viewModel = CreateViewModel(startup);
        viewModel.StartWithWindows.Should().BeFalse();

        viewModel.StartWithWindows = true;
        await viewModel.SaveCommand.ExecuteAsync(null);

        startup.EnableCount.Should().Be(1);
        startup.DisableCount.Should().Be(0);
        viewModel.IsStatusError.Should().BeFalse();
    }

    [Fact]
    public async Task Save_StartWithWindowsUncheckedWhileEnabled_DisablesOnceAndNeverEnables()
    {
        RecordingStartupRegistration startup = new(initiallyEnabled: true);
        SettingsViewModel viewModel = CreateViewModel(startup);
        viewModel.StartWithWindows.Should().BeTrue();

        viewModel.StartWithWindows = false;
        await viewModel.SaveCommand.ExecuteAsync(null);

        startup.DisableCount.Should().Be(1);
        startup.EnableCount.Should().Be(0);
        viewModel.IsStatusError.Should().BeFalse();
    }

    [Fact]
    public async Task Save_StartWithWindowsUnchangedWhileEnabled_TouchesNeitherEnableNorDisable()
    {
        RecordingStartupRegistration startup = new(initiallyEnabled: true);
        SettingsViewModel viewModel = CreateViewModel(startup);
        viewModel.StartWithWindows.Should().BeTrue();

        await viewModel.SaveCommand.ExecuteAsync(null);

        startup.EnableCount.Should().Be(0);
        startup.DisableCount.Should().Be(0);
        viewModel.IsStatusError.Should().BeFalse();
    }

    private static SettingsViewModel CreateViewModel(IStartupRegistration startup) =>
        new(
            new AppSettings(),
            new AcceptingSettingsStore(),
            new StubLocalizationManager(),
            new StubNotificationService(),
            new HotkeyRegistrar(new StubHotkeyService()),
            startup);

    private sealed class RecordingStartupRegistration : IStartupRegistration
    {
        private bool _enabled;

        public RecordingStartupRegistration(bool initiallyEnabled) => _enabled = initiallyEnabled;

        public int EnableCount { get; private set; }

        public int DisableCount { get; private set; }

        public bool IsEnabled() => _enabled;

        public string? GetRegisteredCommand() => _enabled ? "\"C:\\Scrybe\\Scrybe.exe\"" : null;

        public void Enable()
        {
            EnableCount++;
            _enabled = true;
        }

        public void Disable()
        {
            DisableCount++;
            _enabled = false;
        }

        public void HealIfStale()
        {
        }
    }

    private sealed class AcceptingSettingsStore : ISettingsStore
    {
        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SettingsLoadResult(new AppSettings(), true));

        public Task<bool> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class StubHotkeyService : IHotkeyService
    {
        public event EventHandler<string>? HotkeyPressed
        {
            add { }
            remove { }
        }

        public bool TryRegister(string id, HotkeyDefinition definition) => true;

        public void Unregister(string id)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class StubLocalizationManager : ILocalizationManager
    {
        public string this[string key] => key;

        public string Current => "en";

        public event EventHandler? LocaleChanged
        {
            add { }
            remove { }
        }

        public Task LoadAsync(string localeCode, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubNotificationService : INotificationService
    {
        public void Notify(string title, string message)
        {
        }
    }
}
