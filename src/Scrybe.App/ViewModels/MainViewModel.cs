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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrybe.App.Services;
using Scrybe.Core;
using Scrybe.Core.Input;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;

namespace Scrybe.App.ViewModels;

/// <summary>
/// View model for the main control hub. It raises user intents for the application shell to route,
/// while exposing live status from settings and the local libraries.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ILocalizationManager _localization;
    private readonly AppSettings _settings;
    private readonly SnippetLibrary _snippetLibrary;
    private readonly SecretLibrary _secretLibrary;
    private readonly CaptureHistoryLibrary _historyLibrary;
    private readonly AboutInfoProvider _aboutInfoProvider;
    private bool _initialized;
    private bool _suppressCleanupModeEvent;

    /// <summary>Localized application title, shown in the window chrome and header.</summary>
    [ObservableProperty]
    private string _title;

    /// <summary>Localized tagline, shown below the title.</summary>
    [ObservableProperty]
    private string _tagline;

    /// <summary>Human-readable assembly version, for example <c>0.1.0</c>.</summary>
    [ObservableProperty]
    private string _versionText;

    [ObservableProperty]
    private IReadOnlyList<SettingsChoice<OcrCleanupMode>> _cleanupModeChoices;

    [ObservableProperty]
    private OcrCleanupMode _selectedCleanupMode;

    [ObservableProperty]
    private string _captureHotkeyText;

    [ObservableProperty]
    private string _injectHotkeyText;

    [ObservableProperty]
    private string _clipboardInjectHotkeyText;

    [ObservableProperty]
    private string _abortHotkeyText;

    [ObservableProperty]
    private string _snippetPaletteHotkeyText;

    [ObservableProperty]
    private string _secretPaletteHotkeyText;

    [ObservableProperty]
    private string _historyPaletteHotkeyText;

    [ObservableProperty]
    private string _injectionModeText;

    [ObservableProperty]
    private int _snippetCount;

    [ObservableProperty]
    private int _secretCount;

    [ObservableProperty]
    private int _historyCount;

    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>Initializes a new instance using the supplied application state.</summary>
    /// <param name="localization">Source of localized, user-facing strings.</param>
    /// <param name="settings">Live application settings.</param>
    /// <param name="snippetLibrary">Loaded snippet library.</param>
    /// <param name="secretLibrary">Loaded secret library.</param>
    /// <param name="historyLibrary">Loaded protected capture-history library.</param>
    public MainViewModel(
        ILocalizationManager localization,
        AppSettings settings,
        SnippetLibrary snippetLibrary,
        SecretLibrary secretLibrary,
        CaptureHistoryLibrary historyLibrary,
        AboutInfoProvider aboutInfoProvider,
        SettingsViewModel settingsViewModel,
        AboutViewModel aboutViewModel,
        SnippetManagerViewModel snippetManagerViewModel,
        SecretManagerViewModel secretManagerViewModel,
        HistoryManagerViewModel historyManagerViewModel)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(snippetLibrary);
        ArgumentNullException.ThrowIfNull(secretLibrary);
        ArgumentNullException.ThrowIfNull(historyLibrary);
        ArgumentNullException.ThrowIfNull(aboutInfoProvider);
        ArgumentNullException.ThrowIfNull(settingsViewModel);
        ArgumentNullException.ThrowIfNull(aboutViewModel);
        ArgumentNullException.ThrowIfNull(snippetManagerViewModel);
        ArgumentNullException.ThrowIfNull(secretManagerViewModel);
        ArgumentNullException.ThrowIfNull(historyManagerViewModel);

        _localization = localization;
        _settings = settings;
        _snippetLibrary = snippetLibrary;
        _secretLibrary = secretLibrary;
        _historyLibrary = historyLibrary;
        _aboutInfoProvider = aboutInfoProvider;
        Settings = settingsViewModel;
        About = aboutViewModel;
        SnippetManager = snippetManagerViewModel;
        SecretManager = secretManagerViewModel;
        HistoryManager = historyManagerViewModel;
        _title = localization["AppTitle"];
        _tagline = localization["AppTagline"];
        _versionText = aboutInfoProvider.GetAboutInfo().Version;
        _cleanupModeChoices = BuildCleanupModeChoices();
        _selectedCleanupMode = settings.CleanupMode;
        _captureHotkeyText = string.Empty;
        _injectHotkeyText = string.Empty;
        _clipboardInjectHotkeyText = string.Empty;
        _abortHotkeyText = string.Empty;
        _snippetPaletteHotkeyText = string.Empty;
        _secretPaletteHotkeyText = string.Empty;
        _historyPaletteHotkeyText = string.Empty;
        _injectionModeText = string.Empty;
        _selectedTabIndex = ShellTabs.Home;

        localization.LocaleChanged += OnLocaleChanged;
        RefreshStatus();
        _initialized = true;
    }

    /// <summary>Settings tab view model.</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>About tab view model.</summary>
    public AboutViewModel About { get; }

    /// <summary>Snippet manager tab view model.</summary>
    public SnippetManagerViewModel SnippetManager { get; }

    /// <summary>Secret manager tab view model.</summary>
    public SecretManagerViewModel SecretManager { get; }

    /// <summary>Capture history manager tab view model.</summary>
    public HistoryManagerViewModel HistoryManager { get; }

    /// <summary>Raised when the user requests immediate capture from the hub.</summary>
    public event EventHandler? CaptureRequested;

    /// <summary>Raised when the user requests injection of the last OCR text from the hub.</summary>
    public event EventHandler? InjectLastTextRequested;

    /// <summary>Raised when the user requests clipboard injection from the hub.</summary>
    public event EventHandler? ClipboardInjectRequested;

    /// <summary>Raised when the user selects a different cleanup mode from the hub.</summary>
    public event EventHandler<OcrCleanupMode>? CleanupModeChanged;

    /// <summary>Refreshes all live status values from settings and the loaded libraries.</summary>
    public void RefreshStatus()
    {
        CaptureHotkeyText = HotkeyDisplayFormatter.Format(_settings.HotkeyModifiers, _settings.HotkeyKey);
        InjectHotkeyText = HotkeyDisplayFormatter.Format(_settings.InjectHotkeyModifiers, _settings.InjectHotkeyKey);
        ClipboardInjectHotkeyText = HotkeyDisplayFormatter.Format(
            _settings.ClipboardInjectHotkeyModifiers,
            _settings.ClipboardInjectHotkeyKey);
        AbortHotkeyText = HotkeyDisplayFormatter.Format(_settings.AbortHotkeyModifiers, _settings.AbortHotkeyKey);
        SnippetPaletteHotkeyText = HotkeyDisplayFormatter.Format(_settings.PaletteHotkeyModifiers, _settings.PaletteHotkeyKey);
        SecretPaletteHotkeyText = HotkeyDisplayFormatter.Format(_settings.SecretPaletteHotkeyModifiers, _settings.SecretPaletteHotkeyKey);
        HistoryPaletteHotkeyText = HotkeyDisplayFormatter.Format(
            _settings.HistoryPaletteHotkeyModifiers,
            _settings.HistoryPaletteHotkeyKey);
        InjectionModeText = InjectionModeLabel(_settings.InjectionMode);
        SnippetCount = _snippetLibrary.Snippets.Count;
        SecretCount = _secretLibrary.Secrets.Count;
        HistoryCount = _historyLibrary.Entries.Count;
        SynchronizeSelectedCleanupMode();
    }

    /// <summary>Navigate to the home tab.</summary>
    public void ShowHomeTab()
    {
        SelectedTabIndex = ShellTabs.Home;
    }

    /// <summary>Navigate to the snippet manager tab.</summary>
    public void ShowSnippetsTab()
    {
        SelectedTabIndex = ShellTabs.Snippets;
    }

    /// <summary>Navigate to the secret manager tab.</summary>
    public void ShowSecretsTab()
    {
        SelectedTabIndex = ShellTabs.Secrets;
    }

    /// <summary>Navigate to the history manager tab.</summary>
    public void ShowHistoryTab()
    {
        SelectedTabIndex = ShellTabs.History;
    }

    /// <summary>Navigate to the settings tab.</summary>
    public void ShowSettingsTab()
    {
        SelectedTabIndex = ShellTabs.Settings;
    }

    /// <summary>Navigate to the about tab.</summary>
    public void ShowAboutTab()
    {
        SelectedTabIndex = ShellTabs.About;
    }

    [RelayCommand]
    private void Capture() => CaptureRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void InjectLastText() => InjectLastTextRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ClipboardInject() => ClipboardInjectRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenSnippets() => ShowSnippetsTab();

    [RelayCommand]
    private void OpenSecrets() => ShowSecretsTab();

    [RelayCommand]
    private void OpenHistory() => ShowHistoryTab();

    [RelayCommand]
    private void OpenSettings() => ShowSettingsTab();

    [RelayCommand]
    private void OpenAbout() => ShowAboutTab();

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == ShellTabs.History)
        {
            HistoryManager.Reload();
        }

        if (value == ShellTabs.Home)
        {
            RefreshStatus();
        }
    }

    partial void OnSelectedCleanupModeChanged(OcrCleanupMode value)
    {
        if (!_initialized || _suppressCleanupModeEvent)
        {
            return;
        }

        CleanupModeChanged?.Invoke(this, value);
        RefreshStatus();
    }

    private IReadOnlyList<SettingsChoice<OcrCleanupMode>> BuildCleanupModeChoices()
    {
        return
        [
            new SettingsChoice<OcrCleanupMode>(OcrCleanupMode.Raw, _localization["Tray.ModeRaw"]),
            new SettingsChoice<OcrCleanupMode>(OcrCleanupMode.Standard, _localization["Tray.ModeStandard"]),
            new SettingsChoice<OcrCleanupMode>(OcrCleanupMode.LogCleaner, _localization["Tray.ModeLogCleaner"]),
            new SettingsChoice<OcrCleanupMode>(OcrCleanupMode.CodeFormatter, _localization["Tray.ModeCodeFormatter"]),
        ];
    }

    private void OnLocaleChanged(object? sender, EventArgs e)
    {
        Title = _localization["AppTitle"];
        Tagline = _localization["AppTagline"];
        VersionText = _aboutInfoProvider.GetAboutInfo().Version;
        CleanupModeChoices = BuildCleanupModeChoices();
        RefreshStatus();
    }

    private void SynchronizeSelectedCleanupMode()
    {
        _suppressCleanupModeEvent = true;
        try
        {
            SelectedCleanupMode = _settings.CleanupMode;
        }
        finally
        {
            _suppressCleanupModeEvent = false;
        }
    }

    private string InjectionModeLabel(InjectionMode mode) => mode switch
    {
        InjectionMode.Unicode => _localization["Settings.InjectUnicode"],
        InjectionMode.Scancode => _localization["Settings.InjectScancode"],
        _ => mode.ToString(),
    };

}
