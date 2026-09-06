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

using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrybe.App.Services;
using Scrybe.Core;
using Scrybe.Core.Input;
using Scrybe.Core.Interfaces;
using Scrybe.Core.IO;
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
    private readonly INotificationService _notification;
    private readonly HotkeyRegistrar _registrar;
    private readonly IStartupRegistration _startupRegistration;
    private readonly IClipboardService _clipboard;
    private readonly ISystemShell _systemShell;
    private readonly IDirectoryPicker _directoryPicker;
    private readonly bool _initialized;
    private bool _suppressPendingChanges;

    [ObservableProperty]
    private bool _enableLogging;

    [ObservableProperty]
    private bool _prewarmOnStartup;

    [ObservableProperty]
    private bool _startWithWindows;

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
    private bool _clearClipboardAfterInjection;

    [ObservableProperty]
    private int _injectionKeyDelayMs;

    [ObservableProperty]
    private int _selectedInjectionReferenceLength;

    [ObservableProperty]
    private string _capturesDirectory;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private string _injectionIntegrityStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isInjectionIntegrityStatusError;

    [ObservableProperty]
    private bool _hasPendingChanges;

    /// <summary>Initializes the view model from the current settings.</summary>
    /// <param name="settings">The live settings instance.</param>
    /// <param name="store">The persistence store.</param>
    /// <param name="localization">Source of localized strings (and the live locale switch).</param>
    /// <param name="notification">Notification service used when settings cannot be persisted.</param>
    /// <param name="registrar">The hotkey registrar used to re-register edited hotkeys.</param>
    /// <param name="startupRegistration">Registry-backed source of truth for the start-with-Windows entry.</param>
    public SettingsViewModel(
        AppSettings settings,
        ISettingsStore store,
        ILocalizationManager localization,
        INotificationService notification,
        HotkeyRegistrar registrar,
        IStartupRegistration startupRegistration,
        IClipboardService? clipboard = null,
        ISystemShell? systemShell = null,
        IDirectoryPicker? directoryPicker = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(startupRegistration);

        _settings = settings;
        _store = store;
        _localization = localization;
        _notification = notification;
        _registrar = registrar;
        _startupRegistration = startupRegistration;
        _clipboard = clipboard ?? NoopClipboardService.Instance;
        _systemShell = systemShell ?? NoopSystemShell.Instance;
        _directoryPicker = directoryPicker ?? NoopDirectoryPicker.Instance;

        _enableLogging = settings.EnableLogging;
        _prewarmOnStartup = settings.PrewarmOnStartup;
        _startWithWindows = startupRegistration.IsEnabled();
        _enablePreprocessing = settings.EnablePreprocessing;
        _saveCaptureCrop = settings.SaveCaptureCrop;
        _enableCaptureHistory = settings.EnableCaptureHistory;
        _captureHistoryMaxEntries = settings.CaptureHistoryMaxEntries;
        _localeCode = settings.LocaleCode;
        _cleanupMode = settings.CleanupMode;
        _injectionMode = settings.InjectionMode;
        _clearClipboardAfterInjection = settings.ClearClipboardAfterInjection;
        _injectionKeyDelayMs = settings.InjectionKeyDelayMs;
        _selectedInjectionReferenceLength = AppConstants.InjectionReferenceShortLength;
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
        InjectionReferenceLengthChoices =
        [
            new SettingsChoice<int>(AppConstants.InjectionReferenceShortLength, localization["Settings.InjectionReferenceShort"]),
            new SettingsChoice<int>(AppConstants.InjectionReferenceMediumLength, localization["Settings.InjectionReferenceMedium"]),
            new SettingsChoice<int>(AppConstants.InjectionReferenceLongLength, localization["Settings.InjectionReferenceLong"]),
        ];

        CaptureRecorder = new HotkeyRecorderViewModel(
            AppConstants.CaptureHotkeyId, localization["Settings.HotkeyCapture"], settings.HotkeyModifiers, settings.HotkeyKey);
        InjectRecorder = new HotkeyRecorderViewModel(
            AppConstants.InjectHotkeyId, localization["Settings.HotkeyInject"], settings.InjectHotkeyModifiers, settings.InjectHotkeyKey);
        ClipboardInjectRecorder = new HotkeyRecorderViewModel(
            AppConstants.ClipboardInjectHotkeyId,
            localization["Settings.HotkeyClipboardInject"],
            settings.ClipboardInjectHotkeyModifiers,
            settings.ClipboardInjectHotkeyKey);
        AbortRecorder = new HotkeyRecorderViewModel(
            AppConstants.AbortHotkeyId, localization["Settings.HotkeyAbort"], settings.AbortHotkeyModifiers, settings.AbortHotkeyKey);
        PaletteRecorder = new HotkeyRecorderViewModel(
            AppConstants.PaletteHotkeyId, localization["Settings.HotkeyPalette"], settings.PaletteHotkeyModifiers, settings.PaletteHotkeyKey);
        SecretPaletteRecorder = new HotkeyRecorderViewModel(
            AppConstants.SecretPaletteHotkeyId,
            localization["Settings.HotkeySecretPalette"],
            settings.SecretPaletteHotkeyModifiers,
            settings.SecretPaletteHotkeyKey);
        HistoryPaletteRecorder = new HotkeyRecorderViewModel(
            AppConstants.CaptureHistoryHotkeyId,
            localization["Settings.HotkeyHistoryPalette"],
            settings.HistoryPaletteHotkeyModifiers,
            settings.HistoryPaletteHotkeyKey);

        foreach (HotkeyRecorderViewModel recorder in AllRecorders())
        {
            recorder.PropertyChanged += OnRecorderPropertyChanged;
        }

        InitializeFeatures();
        _initialized = true;
    }

    /// <summary>The available locales (English / French).</summary>
    public IReadOnlyList<SettingsChoice<string>> LocaleChoices { get; }

    /// <summary>The available OCR cleanup modes.</summary>
    public IReadOnlyList<SettingsChoice<OcrCleanupMode>> CleanupModeChoices { get; }

    /// <summary>The available injection modes.</summary>
    public IReadOnlyList<SettingsChoice<InjectionMode>> InjectionModeChoices { get; }

    /// <summary>The available integrity-test reference lengths.</summary>
    public IReadOnlyList<SettingsChoice<int>> InjectionReferenceLengthChoices { get; }

    /// <summary>Human-readable accepted range for base injection pacing.</summary>
    public string InjectionKeyDelayRangeText => string.Format(
        CultureInfo.CurrentCulture,
        _localization["Settings.RangeMs"],
        AppConstants.InjectionKeyDelayMinMs,
        AppConstants.InjectionKeyDelayMaxMs);

    /// <summary>Human-readable accepted range for capture-history retention.</summary>
    public string CaptureHistoryMaxEntriesRangeText => string.Format(
        CultureInfo.CurrentCulture,
        _localization["Settings.RangeEntries"],
        AppConstants.CaptureHistoryMaxEntriesMin,
        AppConstants.CaptureHistoryMaxEntriesMax);

    /// <summary>Recorder for the capture hotkey.</summary>
    public HotkeyRecorderViewModel CaptureRecorder { get; }

    /// <summary>Recorder for the inject hotkey.</summary>
    public HotkeyRecorderViewModel InjectRecorder { get; }

    /// <summary>Recorder for the clipboard-injection hotkey.</summary>
    public HotkeyRecorderViewModel ClipboardInjectRecorder { get; }

    /// <summary>Recorder for the abort hotkey.</summary>
    public HotkeyRecorderViewModel AbortRecorder { get; }

    /// <summary>Recorder for the snippet-palette hotkey.</summary>
    public HotkeyRecorderViewModel PaletteRecorder { get; }

    /// <summary>Recorder for the secret-palette hotkey.</summary>
    public HotkeyRecorderViewModel SecretPaletteRecorder { get; }

    /// <summary>Recorder for the capture-history palette hotkey.</summary>
    public HotkeyRecorderViewModel HistoryPaletteRecorder { get; }

    /// <summary>Raised after settings are successfully saved.</summary>
    public event EventHandler? Saved;

    /// <summary>Raised when the user asks the host to inject the selected reference string.</summary>
    public event EventHandler<int>? InjectionReferenceRequested;

    partial void OnLocaleCodeChanged(string value)
    {
        if (!_initialized || string.IsNullOrEmpty(value))
        {
            return;
        }

        MarkPendingChanges();
        _ = _localization.LoadAsync(value);
    }

    partial void OnEnableLoggingChanged(bool value) => MarkPendingChanges();

    partial void OnPrewarmOnStartupChanged(bool value) => MarkPendingChanges();

    partial void OnStartWithWindowsChanged(bool value) => MarkPendingChanges();

    partial void OnEnablePreprocessingChanged(bool value) => MarkPendingChanges();

    partial void OnSaveCaptureCropChanged(bool value) => MarkPendingChanges();

    partial void OnEnableCaptureHistoryChanged(bool value) => MarkPendingChanges();

    partial void OnCaptureHistoryMaxEntriesChanged(int value) => MarkPendingChanges();

    partial void OnCleanupModeChanged(OcrCleanupMode value) => MarkPendingChanges();

    partial void OnInjectionModeChanged(InjectionMode value) => MarkPendingChanges();

    partial void OnClearClipboardAfterInjectionChanged(bool value) => MarkPendingChanges();

    partial void OnInjectionKeyDelayMsChanged(int value) => MarkPendingChanges();

    partial void OnCapturesDirectoryChanged(string value) => MarkPendingChanges();

    [RelayCommand]
    private async Task Save()
    {
        if (ReloadFromDiskCommand.IsRunning) { return; }
        if (!ValidateProfiles()) { return; }
        List<HotkeyBinding> bindings =
        [
            new HotkeyBinding(CaptureRecorder.ActionId, CaptureRecorder.Modifiers, CaptureRecorder.Key),
            new HotkeyBinding(InjectRecorder.ActionId, InjectRecorder.Modifiers, InjectRecorder.Key),
            new HotkeyBinding(ClipboardInjectRecorder.ActionId, ClipboardInjectRecorder.Modifiers, ClipboardInjectRecorder.Key),
            new HotkeyBinding(AbortRecorder.ActionId, AbortRecorder.Modifiers, AbortRecorder.Key),
            new HotkeyBinding(PaletteRecorder.ActionId, PaletteRecorder.Modifiers, PaletteRecorder.Key),
            new HotkeyBinding(SecretPaletteRecorder.ActionId, SecretPaletteRecorder.Modifiers, SecretPaletteRecorder.Key),
            new HotkeyBinding(HistoryPaletteRecorder.ActionId, HistoryPaletteRecorder.Modifiers, HistoryPaletteRecorder.Key),
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
            IsStatusError = true;
            return;
        }

        HotkeyRegistrarResult registration = _registrar.Apply(bindings);
        if (registration.Status == HotkeyRegistrarStatus.RegistrationFailed)
        {
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                _localization["Settings.HotkeyConflict"],
                LabelForAction(registration.FailedActionId));
            IsStatusError = true;
            return;
        }

        _settings.HotkeyModifiers = CaptureRecorder.Modifiers;
        _settings.HotkeyKey = CaptureRecorder.Key;
        _settings.InjectHotkeyModifiers = InjectRecorder.Modifiers;
        _settings.InjectHotkeyKey = InjectRecorder.Key;
        _settings.ClipboardInjectHotkeyModifiers = ClipboardInjectRecorder.Modifiers;
        _settings.ClipboardInjectHotkeyKey = ClipboardInjectRecorder.Key;
        _settings.AbortHotkeyModifiers = AbortRecorder.Modifiers;
        _settings.AbortHotkeyKey = AbortRecorder.Key;
        _settings.PaletteHotkeyModifiers = PaletteRecorder.Modifiers;
        _settings.PaletteHotkeyKey = PaletteRecorder.Key;
        _settings.SecretPaletteHotkeyModifiers = SecretPaletteRecorder.Modifiers;
        _settings.SecretPaletteHotkeyKey = SecretPaletteRecorder.Key;
        _settings.HistoryPaletteHotkeyModifiers = HistoryPaletteRecorder.Modifiers;
        _settings.HistoryPaletteHotkeyKey = HistoryPaletteRecorder.Key;

        _settings.EnableLogging = EnableLogging;
        _settings.ReviewOcrBeforeCopy = ReviewOcrBeforeCopy;
        _settings.InjectionProfiles = Profiles.Select(row => row.Snapshot()).ToList();
        _settings.PrewarmOnStartup = PrewarmOnStartup;
        _settings.EnablePreprocessing = EnablePreprocessing;
        _settings.SaveCaptureCrop = SaveCaptureCrop;
        _settings.EnableCaptureHistory = EnableCaptureHistory;
        _settings.CaptureHistoryMaxEntries = CaptureHistoryMaxEntries;
        _settings.LocaleCode = LocaleCode;
        _settings.CleanupMode = CleanupMode;
        _settings.InjectionMode = InjectionMode;
        _settings.ClearClipboardAfterInjection = ClearClipboardAfterInjection;
        _settings.InjectionKeyDelayMs = InjectionKeyDelayMs;
        _settings.CapturesDirectory = string.IsNullOrWhiteSpace(CapturesDirectory) ? null : CapturesDirectory.Trim();

        SettingsValidator.Validate(_settings);
        _suppressPendingChanges = true;
        try
        {
            InjectionKeyDelayMs = _settings.InjectionKeyDelayMs;
            CaptureHistoryMaxEntries = _settings.CaptureHistoryMaxEntries;
        }
        finally
        {
            _suppressPendingChanges = false;
        }

        FileLogger.SetEnabled(_settings.EnableLogging);
        bool persisted = await _store.SaveAsync(_settings).ConfigureAwait(true);
        if (!persisted)
        {
            ShowSaveFailure();
            FileLogger.Warn("Settings updated in memory but could not be persisted.");
            return;
        }

        FileLogger.Info("Settings saved.");

        if (!TryApplyStartupRegistration())
        {
            // The JSON save already succeeded; surface the registry error without rolling it back.
            return;
        }

        StatusMessage = _localization["Settings.Saved"];
        IsStatusError = false;
        HasPendingChanges = false;
        Saved?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task CopyInjectionReference()
    {
        string reference = ReferenceText.OfLength(SelectedInjectionReferenceLength);
        await _clipboard.SetTextAsync(reference).ConfigureAwait(true);
        InjectionIntegrityStatusMessage = string.Format(
            CultureInfo.CurrentCulture,
            _localization["Settings.InjectionReferenceCopied"],
            reference.Length);
        IsInjectionIntegrityStatusError = false;
    }

    [RelayCommand]
    private void InjectReference()
    {
        InjectionReferenceRequested?.Invoke(this, SelectedInjectionReferenceLength);
        InjectionIntegrityStatusMessage = string.Format(
            CultureInfo.CurrentCulture,
            _localization["Settings.InjectionReferenceQueued"],
            SelectedInjectionReferenceLength,
            InjectionMode);
        IsInjectionIntegrityStatusError = false;
    }

    [RelayCommand]
    private void BrowseCapturesDirectory()
    {
        if (_directoryPicker.TryPickDirectory(CapturesDirectory, out string directory))
        {
            CapturesDirectory = directory;
        }
    }

    [RelayCommand]
    private void OpenCapturesDirectory()
    {
        string directory = CapturePathResolver.ResolveDirectory(CapturesDirectory);
        if (_systemShell.TryOpenDirectory(directory))
        {
            StatusMessage = _localization["Settings.CapturesOpened"];
            IsStatusError = false;
            return;
        }

        StatusMessage = string.Format(CultureInfo.CurrentCulture, _localization["Diagnostics.OpenFailed"], directory);
        IsStatusError = true;
    }

    [RelayCommand]
    private void ResetCapturesDirectory()
    {
        CapturesDirectory = string.Empty;
        StatusMessage = _localization["Settings.CapturesDirectoryReset"];
        IsStatusError = false;
    }

    [RelayCommand]
    private void DecreaseInjectionKeyDelay()
        => InjectionKeyDelayMs = Math.Clamp(
            InjectionKeyDelayMs - 5,
            AppConstants.InjectionKeyDelayMinMs,
            AppConstants.InjectionKeyDelayMaxMs);

    [RelayCommand]
    private void IncreaseInjectionKeyDelay()
        => InjectionKeyDelayMs = Math.Clamp(
            InjectionKeyDelayMs + 5,
            AppConstants.InjectionKeyDelayMinMs,
            AppConstants.InjectionKeyDelayMaxMs);

    [RelayCommand]
    private void DecreaseCaptureHistoryMaxEntries()
        => CaptureHistoryMaxEntries = Math.Clamp(
            CaptureHistoryMaxEntries - 5,
            AppConstants.CaptureHistoryMaxEntriesMin,
            AppConstants.CaptureHistoryMaxEntriesMax);

    [RelayCommand]
    private void IncreaseCaptureHistoryMaxEntries()
        => CaptureHistoryMaxEntries = Math.Clamp(
            CaptureHistoryMaxEntries + 5,
            AppConstants.CaptureHistoryMaxEntriesMin,
            AppConstants.CaptureHistoryMaxEntriesMax);

    private bool TryApplyStartupRegistration()
    {
        try
        {
            bool currentlyEnabled = _startupRegistration.IsEnabled();
            if (StartWithWindows && !currentlyEnabled)
            {
                _startupRegistration.Enable();
            }
            else if (!StartWithWindows && currentlyEnabled)
            {
                _startupRegistration.Disable();
            }

            return true;
        }
        catch (Exception exception)
        {
            FileLogger.Error("Failed to apply the start-with-Windows registry state.", exception);
            StatusMessage = _localization["Persist.SaveFailed"];
            IsStatusError = true;
            HasPendingChanges = true;
            return false;
        }
    }

    private void ShowSaveFailure()
    {
        string message = _localization["Persist.SaveFailed"];
        StatusMessage = message;
        IsStatusError = true;
        HasPendingChanges = true;
        _notification.Notify(_localization["AppTitle"], message);
    }

    private IReadOnlyList<HotkeyRecorderViewModel> AllRecorders() =>
    [
        CaptureRecorder,
        InjectRecorder,
        ClipboardInjectRecorder,
        AbortRecorder,
        PaletteRecorder,
        SecretPaletteRecorder,
        HistoryPaletteRecorder,
    ];

    private void OnRecorderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HotkeyRecorderViewModel.Modifiers) or nameof(HotkeyRecorderViewModel.Key))
        {
            MarkPendingChanges();
        }
    }

    private void MarkPendingChanges()
    {
        if (!_initialized || _suppressPendingChanges)
        {
            return;
        }

        HasPendingChanges = HasUnsavedChanges();
        if (HasPendingChanges && !IsStatusError)
        {
            StatusMessage = string.Empty;
        }
    }

    private bool HasUnsavedChanges()
    {
        string? capturesDirectory = string.IsNullOrWhiteSpace(CapturesDirectory) ? null : CapturesDirectory.Trim();

        return EnableLogging != _settings.EnableLogging
            || PrewarmOnStartup != _settings.PrewarmOnStartup
            || StartWithWindows != _startupRegistration.IsEnabled()
            || EnablePreprocessing != _settings.EnablePreprocessing
            || SaveCaptureCrop != _settings.SaveCaptureCrop
            || EnableCaptureHistory != _settings.EnableCaptureHistory
            || CaptureHistoryMaxEntries != _settings.CaptureHistoryMaxEntries
            || !Same(LocaleCode, _settings.LocaleCode)
            || CleanupMode != _settings.CleanupMode
            || InjectionMode != _settings.InjectionMode
            || ClearClipboardAfterInjection != _settings.ClearClipboardAfterInjection
            || InjectionKeyDelayMs != _settings.InjectionKeyDelayMs
            || !Same(capturesDirectory, _settings.CapturesDirectory)
            || !Same(CaptureRecorder.Modifiers, _settings.HotkeyModifiers)
            || !Same(CaptureRecorder.Key, _settings.HotkeyKey)
            || !Same(InjectRecorder.Modifiers, _settings.InjectHotkeyModifiers)
            || !Same(InjectRecorder.Key, _settings.InjectHotkeyKey)
            || !Same(ClipboardInjectRecorder.Modifiers, _settings.ClipboardInjectHotkeyModifiers)
            || !Same(ClipboardInjectRecorder.Key, _settings.ClipboardInjectHotkeyKey)
            || !Same(AbortRecorder.Modifiers, _settings.AbortHotkeyModifiers)
            || !Same(AbortRecorder.Key, _settings.AbortHotkeyKey)
            || !Same(PaletteRecorder.Modifiers, _settings.PaletteHotkeyModifiers)
            || !Same(PaletteRecorder.Key, _settings.PaletteHotkeyKey)
            || !Same(SecretPaletteRecorder.Modifiers, _settings.SecretPaletteHotkeyModifiers)
            || !Same(SecretPaletteRecorder.Key, _settings.SecretPaletteHotkeyKey)
            || !Same(HistoryPaletteRecorder.Modifiers, _settings.HistoryPaletteHotkeyModifiers)
            || !Same(HistoryPaletteRecorder.Key, _settings.HistoryPaletteHotkeyKey);
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left, right, StringComparison.Ordinal);

    private string LabelForAction(string? actionId) => actionId switch
    {
        AppConstants.CaptureHotkeyId => _localization["Settings.HotkeyCapture"],
        AppConstants.InjectHotkeyId => _localization["Settings.HotkeyInject"],
        AppConstants.ClipboardInjectHotkeyId => _localization["Settings.HotkeyClipboardInject"],
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

    private sealed class NoopClipboardService : IClipboardService
    {
        public static NoopClipboardService Instance { get; } = new();

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoopSystemShell : ISystemShell
    {
        public static NoopSystemShell Instance { get; } = new();

        public bool TryOpenDirectory(string directory) => false;
    }

    private sealed class NoopDirectoryPicker : IDirectoryPicker
    {
        public static NoopDirectoryPicker Instance { get; } = new();

        public bool TryPickDirectory(string? initialDirectory, out string directory)
        {
            directory = string.Empty;
            return false;
        }
    }
}
