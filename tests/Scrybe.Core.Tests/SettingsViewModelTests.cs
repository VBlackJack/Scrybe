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
using Scrybe.Core;
using Scrybe.Core.Input;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for settings saves that rebind every user-editable global hotkey.</summary>
public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task Save_WithEditedClipboardAndHistoryHotkeys_PersistsAndRegistersSevenBindings()
    {
        AppSettings settings = new();
        InMemorySettingsStore store = new();
        FakeHotkeyService hotkeys = new();
        HotkeyRegistrar registrar = new(hotkeys);
        TestNotificationService notification = new();
        SettingsViewModel viewModel = new(settings, store, new TestLocalizationManager(), notification, registrar, new NoopStartupRegistration());

        viewModel.ClipboardInjectRecorder.Capture("Control+Alt", "Y");
        viewModel.HistoryPaletteRecorder.Capture("Control+Alt", "U");

        await viewModel.SaveCommand.ExecuteAsync(null);

        store.SaveCount.Should().Be(1);
        store.SavedSettings.Should().NotBeNull();
        store.SavedSettings!.ClipboardInjectHotkeyModifiers.Should().Be("Control+Alt");
        store.SavedSettings.ClipboardInjectHotkeyKey.Should().Be("Y");
        store.SavedSettings.HistoryPaletteHotkeyModifiers.Should().Be("Control+Alt");
        store.SavedSettings.HistoryPaletteHotkeyKey.Should().Be("U");
        hotkeys.Registered.Should().HaveCount(7);
        hotkeys.Registered[AppConstants.ClipboardInjectHotkeyId].DisplayName.Should().Be("Control+Alt+Y");
        hotkeys.Registered[AppConstants.CaptureHistoryHotkeyId].DisplayName.Should().Be("Control+Alt+U");
        registrar.Current.Should().HaveCount(7);
        viewModel.StatusMessage.Should().Be("Saved");
        viewModel.IsStatusError.Should().BeFalse();
        notification.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_WithDuplicateClipboardAndHistoryHotkeys_RejectsBeforePersisting()
    {
        AppSettings settings = new();
        InMemorySettingsStore store = new();
        FakeHotkeyService hotkeys = new();
        HotkeyRegistrar registrar = new(hotkeys);
        SettingsViewModel viewModel = new(settings, store, new TestLocalizationManager(), new TestNotificationService(), registrar, new NoopStartupRegistration());

        viewModel.HistoryPaletteRecorder.Capture("Alt+Control", "B");

        await viewModel.SaveCommand.ExecuteAsync(null);

        store.SaveCount.Should().Be(0);
        hotkeys.Registered.Should().BeEmpty();
        registrar.Current.Should().BeEmpty();
        settings.HistoryPaletteHotkeyKey.Should().Be(AppConstants.DefaultHistoryPaletteHotkeyKey);
        viewModel.StatusMessage.Should().Contain("duplicate");
        viewModel.IsStatusError.Should().BeTrue();
    }

    [Fact]
    public async Task Save_WhenStoreFails_NotifiesAndDoesNotRaiseSaved()
    {
        AppSettings settings = new();
        InMemorySettingsStore store = new()
        {
            SaveSucceeds = false,
        };
        FakeHotkeyService hotkeys = new();
        HotkeyRegistrar registrar = new(hotkeys);
        TestNotificationService notification = new();
        SettingsViewModel viewModel = new(settings, store, new TestLocalizationManager(), notification, registrar, new NoopStartupRegistration());
        int savedEvents = 0;
        viewModel.Saved += (_, _) => savedEvents++;

        await viewModel.SaveCommand.ExecuteAsync(null);

        store.SaveCount.Should().Be(1);
        viewModel.StatusMessage.Should().Be("Couldn't save changes");
        viewModel.IsStatusError.Should().BeTrue();
        notification.Messages.Should().ContainSingle("Couldn't save changes");
        savedEvents.Should().Be(0);
    }

    [Fact]
    public async Task CopyInjectionReference_CopiesSelectedReferenceText()
    {
        TestClipboardService clipboard = new();
        SettingsViewModel viewModel = new(
            new AppSettings(),
            new InMemorySettingsStore(),
            new TestLocalizationManager(),
            new TestNotificationService(),
            new HotkeyRegistrar(new FakeHotkeyService()),
            new NoopStartupRegistration(),
            clipboard);

        viewModel.SelectedInjectionReferenceLength = AppConstants.InjectionReferenceMediumLength;

        await viewModel.CopyInjectionReferenceCommand.ExecuteAsync(null);

        clipboard.Text.Should().Be(ReferenceText.OfLength(AppConstants.InjectionReferenceMediumLength));
        viewModel.InjectionIntegrityStatusMessage.Should().Be("500 reference characters copied");
        viewModel.IsInjectionIntegrityStatusError.Should().BeFalse();
    }

    [Fact]
    public void InjectReference_RaisesSelectedReferenceLength()
    {
        SettingsViewModel viewModel = new(
            new AppSettings(),
            new InMemorySettingsStore(),
            new TestLocalizationManager(),
            new TestNotificationService(),
            new HotkeyRegistrar(new FakeHotkeyService()),
            new NoopStartupRegistration());
        int requestedLength = 0;
        viewModel.InjectionReferenceRequested += (_, length) => requestedLength = length;

        viewModel.SelectedInjectionReferenceLength = AppConstants.InjectionReferenceLongLength;
        viewModel.InjectReferenceCommand.Execute(null);

        requestedLength.Should().Be(AppConstants.InjectionReferenceLongLength);
        viewModel.InjectionIntegrityStatusMessage.Should().Be("1000-character Unicode test queued");
        viewModel.IsInjectionIntegrityStatusError.Should().BeFalse();
    }

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        public int SaveCount { get; private set; }

        public AppSettings? SavedSettings { get; private set; }

        public bool SaveSucceeds { get; init; } = true;

        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SettingsLoadResult(new AppSettings(), false));

        public Task<bool> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (!SaveSucceeds)
            {
                return Task.FromResult(false);
            }

            SavedSettings = settings;
            return Task.FromResult(true);
        }
    }

    private sealed class TestLocalizationManager : ILocalizationManager
    {
        public string this[string key] => key switch
        {
            "Settings.LangEnglish" => "English",
            "Settings.LangFrench" => "French",
            "Tray.ModeRaw" => "Raw",
            "Tray.ModeStandard" => "Standard",
            "Tray.ModeLogCleaner" => "Log cleaner",
            "Tray.ModeCodeFormatter" => "Code formatter",
            "Settings.InjectUnicode" => "Unicode",
            "Settings.InjectScancode" => "Scancode",
            "Settings.HotkeyCapture" => "Capture",
            "Settings.HotkeyInject" => "Inject",
            "Settings.HotkeyClipboardInject" => "Clipboard injection",
            "Settings.HotkeyAbort" => "Abort",
            "Settings.HotkeyPalette" => "Snippet palette",
            "Settings.HotkeySecretPalette" => "Secret palette",
            "Settings.HotkeyHistoryPalette" => "Capture history",
            "Settings.HotkeyInvalid" => "{0}: {1}",
            "Settings.HotkeyConflict" => "{0}: conflict",
            "Settings.HotkeyReasonDuplicate" => "duplicate",
            "Settings.HotkeyReasonNoModifier" => "missing modifier",
            "Settings.HotkeyReasonUnparseable" => "unparseable",
            "Settings.Saved" => "Saved",
            "Settings.InjectionReferenceShort" => "100 chars",
            "Settings.InjectionReferenceMedium" => "500 chars",
            "Settings.InjectionReferenceLong" => "1000 chars",
            "Settings.InjectionReferenceCopied" => "{0} reference characters copied",
            "Settings.InjectionReferenceQueued" => "{0}-character {1} test queued",
            "Persist.SaveFailed" => "Couldn't save changes",
            "AppTitle" => "Scrybe",
            _ => key,
        };

        public string Current => "en";

        public event EventHandler? LocaleChanged
        {
            add { }
            remove { }
        }

        public Task LoadAsync(string localeCode, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeHotkeyService : IHotkeyService
    {
#pragma warning disable CS0067 // Required by IHotkeyService; the fake never raises it.
        public event EventHandler<string>? HotkeyPressed;
#pragma warning restore CS0067

        public Dictionary<string, HotkeyDefinition> Registered { get; } = new(StringComparer.Ordinal);

        public bool TryRegister(string id, HotkeyDefinition definition)
        {
            Registered[id] = definition;
            return true;
        }

        public void Unregister(string id) => Registered.Remove(id);

        public void Dispose()
        {
        }
    }

    private sealed class TestNotificationService : INotificationService
    {
        public List<string> Messages { get; } = [];

        public void Notify(string title, string message)
            => Messages.Add(message);
    }

    private sealed class TestClipboardService : IClipboardService
    {
        public string Text { get; private set; } = string.Empty;

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(Text);

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            Text = text;
            return Task.CompletedTask;
        }
    }

    private sealed class NoopStartupRegistration : IStartupRegistration
    {
        public bool IsEnabled() => false;

        public string? GetRegisteredCommand() => null;

        public void Enable()
        {
        }

        public void Disable()
        {
        }

        public void HealIfStale()
        {
        }
    }
}
