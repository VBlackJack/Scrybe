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

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrybe.Core;
using Scrybe.Core.Input;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;
using Scrybe.Core.Settings;

namespace Scrybe.App.ViewModels;

/// <summary>
/// View model for the settings window. Edits every setting; the locale is applied live, the rest take
/// effect on save. Global hotkeys are re-bindable: on save they are validated, then re-registered with
/// the OS, rolling back to the previous working set if a combo is already in use.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly ISettingsStore _store;
    private readonly ILocalizationManager _localization;
    private readonly HotkeyRegistrar _registrar;
    private readonly bool _initialized;

    [ObservableProperty]
    private bool _enableLogging;

    [ObservableProperty]
    private bool _prewarmOnStartup;

    [ObservableProperty]
    private bool _enablePreprocessing;

    [ObservableProperty]
    private bool _saveCaptureCrop;

    [ObservableProperty]
    private bool _enableCaptureHistory;

    [ObservableProperty]
    private int _captureHistoryMaxEntries;

    [ObservableProperty]
    private string _localeCode;

    [ObservableProperty]
    private OcrCleanupMode _cleanupMode;

    [ObservableProperty]
    private InjectionMode _injectionMode;

    [ObservableProperty]
    private int _injectionKeyDelayMs;

    [ObservableProperty]
    private string _capturesDirectory;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Initializes the view model from the current settings.</summary>
    /// <param name="settings">The live settings instance.</param>
    /// <param name="store">The persistence store.</param>
    /// <param name="localization">Source of localized strings (and the live locale switch).</param>
    /// <param name="registrar">The hotkey registrar used to re-register edited hotkeys.</param>
    public SettingsViewModel(
        AppSettings settings,
        ISettingsStore store,
        ILocalizationManager localization,
        HotkeyRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(registrar);

        _settings = settings;
        _store = store;
        _localization = localization;
        _registrar = registrar;

        _enableLogging = settings.EnableLogging;
        _prewarmOnStartup = settings.PrewarmOnStartup;
        _enablePreprocessing = settings.EnablePreprocessing;
        _saveCaptureCrop = settings.SaveCaptureCrop;
        _enableCaptureHistory = settings.EnableCaptureHistory;
        _captureHistoryMaxEntries = settings.CaptureHistoryMaxEntries;
        _localeCode = settings.LocaleCode;
        _cleanupMode = settings.CleanupMode;
        _injectionMode = settings.InjectionMode;
        _injectionKeyDelayMs = settings.InjectionKeyDelayMs;
        _capturesDirectory = settings.CapturesDirectory ?? string.Empty;

        LocaleChoices =
        [
            new SettingsChoice<string>("en", localization["Settings.LangEnglish"]),
            new SettingsChoice<string>("fr", localization["Settings.LangFrench"]),
        ];
        CleanupModeChoices =
        [
            new SettingsChoice<OcrCleanupMode>(OcrCleanupMode.Raw, localization["Tray.ModeRaw"]),
            new SettingsChoice<OcrCleanupMode>(OcrCleanupMode.Standard, localization["Tray.ModeStandard"]),
            new SettingsChoice<OcrCleanupMode>(OcrCleanupMode.LogCleaner, localization["Tray.ModeLogCleaner"]),
            new SettingsChoice<OcrCleanupMode>(OcrCleanupMode.CodeFormatter, localization["Tray.ModeCodeFormatter"]),
        ];
        InjectionModeChoices =
        [
            new SettingsChoice<InjectionMode>(InjectionMode.Unicode, localization["Settings.InjectUnicode"]),
            new SettingsChoice<InjectionMode>(InjectionMode.Scancode, localization["Settings.InjectScancode"]),
        ];

        CaptureRecorder = new HotkeyRecorderViewModel(
            AppConstants.CaptureHotkeyId, localization["Settings.HotkeyCapture"], settings.HotkeyModifiers, settings.HotkeyKey);
        InjectRecorder = new HotkeyRecorderViewModel(
            AppConstants.InjectHotkeyId, localization["Settings.HotkeyInject"], settings.InjectHotkeyModifiers, settings.InjectHotkeyKey);
        AbortRecorder = new HotkeyRecorderViewModel(
            AppConstants.AbortHotkeyId, localization["Settings.HotkeyAbort"], settings.AbortHotkeyModifiers, settings.AbortHotkeyKey);
        PaletteRecorder = new HotkeyRecorderViewModel(
            AppConstants.PaletteHotkeyId, localization["Settings.HotkeyPalette"], settings.PaletteHotkeyModifiers, settings.PaletteHotkeyKey);
        SecretPaletteRecorder = new HotkeyRecorderViewModel(
            AppConstants.SecretPaletteHotkeyId,
            localization["Settings.HotkeySecretPalette"],
            settings.SecretPaletteHotkeyModifiers,
            settings.SecretPaletteHotkeyKey);

        _initialized = true;
    }

    /// <summary>The available locales (English / French).</summary>
    public IReadOnlyList<SettingsChoice<string>> LocaleChoices { get; }

    /// <summary>The available OCR cleanup modes.</summary>
    public IReadOnlyList<SettingsChoice<OcrCleanupMode>> CleanupModeChoices { get; }

    /// <summary>The available injection modes.</summary>
    public IReadOnlyList<SettingsChoice<InjectionMode>> InjectionModeChoices { get; }

    /// <summary>Recorder for the capture hotkey.</summary>
    public HotkeyRecorderViewModel CaptureRecorder { get; }

    /// <summary>Recorder for the inject hotkey.</summary>
    public HotkeyRecorderViewModel InjectRecorder { get; }

    /// <summary>Recorder for the abort hotkey.</summary>
    public HotkeyRecorderViewModel AbortRecorder { get; }

    /// <summary>Recorder for the snippet-palette hotkey.</summary>
    public HotkeyRecorderViewModel PaletteRecorder { get; }

    /// <summary>Recorder for the secret-palette hotkey.</summary>
    public HotkeyRecorderViewModel SecretPaletteRecorder { get; }

    /// <summary>Raised after settings are successfully saved.</summary>
    public event EventHandler? Saved;

    partial void OnLocaleCodeChanged(string value)
    {
        if (!_initialized || string.IsNullOrEmpty(value))
        {
            return;
        }

        _ = _localization.LoadAsync(value);
    }

    [RelayCommand]
    private async Task Save()
    {
        List<HotkeyBinding> bindings =
        [
            new HotkeyBinding(CaptureRecorder.ActionId, CaptureRecorder.Modifiers, CaptureRecorder.Key),
            new HotkeyBinding(InjectRecorder.ActionId, InjectRecorder.Modifiers, InjectRecorder.Key),
            new HotkeyBinding(AbortRecorder.ActionId, AbortRecorder.Modifiers, AbortRecorder.Key),
            new HotkeyBinding(PaletteRecorder.ActionId, PaletteRecorder.Modifiers, PaletteRecorder.Key),
            new HotkeyBinding(SecretPaletteRecorder.ActionId, SecretPaletteRecorder.Modifiers, SecretPaletteRecorder.Key),
            new HotkeyBinding(
                AppConstants.CaptureHistoryHotkeyId,
                _settings.HistoryPaletteHotkeyModifiers,
                _settings.HistoryPaletteHotkeyKey),
        ];

        HotkeyBindingValidationResult validation = HotkeyBindingValidator.Validate(bindings);
        if (!validation.IsValid)
        {
            HotkeyBindingValidationEntry firstError = validation.Errors[0];
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                _localization["Settings.HotkeyInvalid"],
                LabelForAction(firstError.ActionId),
                ReasonText(firstError.Error));
            return;
        }

        HotkeyRegistrarResult registration = _registrar.Apply(bindings);
        if (registration.Status == HotkeyRegistrarStatus.RegistrationFailed)
        {
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                _localization["Settings.HotkeyConflict"],
                LabelForAction(registration.FailedActionId));
            return;
        }

        _settings.HotkeyModifiers = CaptureRecorder.Modifiers;
        _settings.HotkeyKey = CaptureRecorder.Key;
        _settings.InjectHotkeyModifiers = InjectRecorder.Modifiers;
        _settings.InjectHotkeyKey = InjectRecorder.Key;
        _settings.AbortHotkeyModifiers = AbortRecorder.Modifiers;
        _settings.AbortHotkeyKey = AbortRecorder.Key;
        _settings.PaletteHotkeyModifiers = PaletteRecorder.Modifiers;
        _settings.PaletteHotkeyKey = PaletteRecorder.Key;
        _settings.SecretPaletteHotkeyModifiers = SecretPaletteRecorder.Modifiers;
        _settings.SecretPaletteHotkeyKey = SecretPaletteRecorder.Key;

        _settings.EnableLogging = EnableLogging;
        _settings.PrewarmOnStartup = PrewarmOnStartup;
        _settings.EnablePreprocessing = EnablePreprocessing;
        _settings.SaveCaptureCrop = SaveCaptureCrop;
        _settings.EnableCaptureHistory = EnableCaptureHistory;
        _settings.CaptureHistoryMaxEntries = CaptureHistoryMaxEntries;
        _settings.LocaleCode = LocaleCode;
        _settings.CleanupMode = CleanupMode;
        _settings.InjectionMode = InjectionMode;
        _settings.InjectionKeyDelayMs = InjectionKeyDelayMs;
        _settings.CapturesDirectory = string.IsNullOrWhiteSpace(CapturesDirectory) ? null : CapturesDirectory.Trim();

        SettingsValidator.Validate(_settings);
        InjectionKeyDelayMs = _settings.InjectionKeyDelayMs;
        CaptureHistoryMaxEntries = _settings.CaptureHistoryMaxEntries;

        FileLogger.SetEnabled(_settings.EnableLogging);
        await _store.SaveAsync(_settings).ConfigureAwait(true);
        FileLogger.Info("Settings saved.");

        StatusMessage = _localization["Settings.Saved"];
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private string LabelForAction(string? actionId) => actionId switch
    {
        AppConstants.CaptureHotkeyId => _localization["Settings.HotkeyCapture"],
        AppConstants.InjectHotkeyId => _localization["Settings.HotkeyInject"],
        AppConstants.AbortHotkeyId => _localization["Settings.HotkeyAbort"],
        AppConstants.PaletteHotkeyId => _localization["Settings.HotkeyPalette"],
        AppConstants.SecretPaletteHotkeyId => _localization["Settings.HotkeySecretPalette"],
        AppConstants.CaptureHistoryHotkeyId => _localization["Settings.HotkeyHistoryPalette"],
        _ => actionId ?? string.Empty,
    };

    private string ReasonText(HotkeyBindingError error) => error switch
    {
        HotkeyBindingError.Duplicate => _localization["Settings.HotkeyReasonDuplicate"],
        HotkeyBindingError.MissingModifier => _localization["Settings.HotkeyReasonNoModifier"],
        _ => _localization["Settings.HotkeyReasonUnparseable"],
    };
}
