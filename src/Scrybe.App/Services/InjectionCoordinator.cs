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

using System.Diagnostics;
using System.Globalization;
using Scrybe.Core;
using Scrybe.Core.Input;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;

namespace Scrybe.App.Services;

/// <summary>
/// Coordinates injection: builds the key sequence for either the last OCR text or a known reference
/// string and types it through the active strategy (Unicode or scancode). A single injection runs at a
/// time, with an emergency abort, a runtime mode toggle for the integrity A/B, and localized confirmations.
/// </summary>
public sealed class InjectionCoordinator
{
    private readonly UnicodeInjector _unicodeInjector;
    private readonly ScancodeInjector _scancodeInjector;
    private readonly IOcrTextStore _textStore;
    private readonly INotificationService _notification;
    private readonly ILocalizationManager _localization;

    private readonly AppSettings _settings;

    private CancellationTokenSource? _activeInjection;
    private int _injectionInProgress;

    /// <summary>Initializes the coordinator with both injection strategies and its dependencies.</summary>
    /// <param name="unicodeInjector">The Unicode injection strategy.</param>
    /// <param name="scancodeInjector">The scancode injection strategy.</param>
    /// <param name="textStore">Store providing the last OCR text to type.</param>
    /// <param name="notification">Notification service for confirmations and warnings.</param>
    /// <param name="localization">Source of localized messages.</param>
    /// <param name="settings">Application settings; the live source of the active injection mode and pacing.</param>
    public InjectionCoordinator(
        UnicodeInjector unicodeInjector,
        ScancodeInjector scancodeInjector,
        IOcrTextStore textStore,
        INotificationService notification,
        ILocalizationManager localization,
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(unicodeInjector);
        ArgumentNullException.ThrowIfNull(scancodeInjector);
        ArgumentNullException.ThrowIfNull(textStore);
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(settings);

        _unicodeInjector = unicodeInjector;
        _scancodeInjector = scancodeInjector;
        _textStore = textStore;
        _notification = notification;
        _localization = localization;
        _settings = settings;
    }

    private IKeystrokeInjector CurrentInjector
        => _settings.InjectionMode == InjectionMode.Unicode ? _unicodeInjector : _scancodeInjector;

    /// <summary>Types the last OCR text into the foreground window. Ignored if an injection is already running.</summary>
    public Task InjectLastTextAsync()
    {
        string? text = _textStore.LastText;
        if (string.IsNullOrEmpty(text))
        {
            FileLogger.Info("Inject requested but no OCR text is available.");
            _notification.Notify(_localization["AppTitle"], _localization["Inject.Nothing"]);
            return Task.CompletedTask;
        }

        return RunInjectionAsync(text, "OCR text");
    }

    /// <summary>Injects the known reference string of the given length (integrity A/B harness).</summary>
    /// <param name="length">The reference length to inject.</param>
    public Task InjectReferenceAsync(int length) => RunInjectionAsync(ReferenceText.OfLength(length), $"reference[{length}]");

    /// <summary>Injects arbitrary resolved text (for example a filled snippet) into the foreground window.</summary>
    /// <param name="text">The text to type.</param>
    public Task InjectTextAsync(string text) => RunInjectionAsync(text, "snippet");

    /// <summary>Injects a secret into the foreground window without logging the value or using the clipboard.</summary>
    /// <param name="text">The secret text to type.</param>
    public Task InjectSecretAsync(char[] text) => RunSecretInjectionAsync(text);

    /// <summary>Switches the active injection strategy and reports the new mode.</summary>
    public void ToggleMode()
    {
        _settings.InjectionMode = _settings.InjectionMode == InjectionMode.Unicode
            ? InjectionMode.Scancode
            : InjectionMode.Unicode;
        FileLogger.Info($"Injection mode switched to {_settings.InjectionMode}.");
        _notification.Notify(
            _localization["AppTitle"],
            string.Format(CultureInfo.CurrentCulture, _localization["Inject.Mode"], _settings.InjectionMode));
    }

    /// <summary>Cycles the debug inter-key pacing preset used by the remote integrity harness.</summary>
    public void CyclePacing()
    {
        int[] presets = AppConstants.DebugInjectionPacingPresetsMs;
        int current = _settings.InjectionKeyDelayMs;
        int next = presets.FirstOrDefault(preset => preset > current);
        if (next == 0)
        {
            next = presets[0];
        }

        _settings.InjectionKeyDelayMs = next;
        FileLogger.Info($"Injection pacing switched to {next}ms.");
        _notification.Notify(
            _localization["AppTitle"],
            string.Format(CultureInfo.CurrentCulture, _localization["Inject.Pacing"], next));
    }

    /// <summary>Cancels an in-progress injection immediately (emergency abort).</summary>
    public void Abort()
    {
        CancellationTokenSource? cancellation = _activeInjection;
        if (cancellation is not null)
        {
            FileLogger.Info("Injection abort requested.");
            cancellation.Cancel();
        }
    }

    private async Task RunInjectionAsync(string text, string label)
    {
        if (Interlocked.CompareExchange(ref _injectionInProgress, 1, 0) != 0)
        {
            FileLogger.Warn("Injection already in progress; ignoring trigger.");
            return;
        }

        CancellationTokenSource cancellation = new();
        _activeInjection = cancellation;

        try
        {
            KeystrokeSequence sequence = KeystrokeBuilder.Build(text);
            FileLogger.Info(
                $"Injecting {label} via {_settings.InjectionMode}: {sequence.Strokes.Count} keystrokes, {sequence.SkippedCharacters} chars skipped.");

            long startTimestamp = Stopwatch.GetTimestamp();
            InjectionResult result = await CurrentInjector.InjectAsync(sequence, cancellation.Token).ConfigureAwait(false);
            double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

            HandleResult(result, sequence.SkippedCharacters, elapsedMs);
        }
        catch (Exception exception)
        {
            FileLogger.Error("Injection failed.", exception);
            _notification.Notify(_localization["AppTitle"], _localization["Inject.Failed"]);
        }
        finally
        {
            _activeInjection = null;
            cancellation.Dispose();
            Interlocked.Exchange(ref _injectionInProgress, 0);
        }
    }

    private async Task RunSecretInjectionAsync(char[] text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (Interlocked.CompareExchange(ref _injectionInProgress, 1, 0) != 0)
        {
            FileLogger.Warn("Injection already in progress; ignoring trigger.");
            return;
        }

        CancellationTokenSource cancellation = new();
        _activeInjection = cancellation;

        try
        {
            int plannedStrokes = KeystrokeBuilder.CountStrokes(text.AsSpan());
            int skippedCharacters = KeystrokeBuilder.CountSkipped(text.AsSpan());
            FileLogger.Info(
                $"Injecting secret via {_settings.InjectionMode}: {plannedStrokes} keystrokes, {skippedCharacters} chars skipped.");

            long startTimestamp = Stopwatch.GetTimestamp();
            InjectionResult result = await CurrentInjector.InjectAsync(text.AsMemory(), cancellation.Token).ConfigureAwait(false);
            double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

            HandleResult(result, skippedCharacters, elapsedMs);
        }
        catch (Exception exception)
        {
            FileLogger.Error("Injection failed.", exception);
            _notification.Notify(_localization["AppTitle"], _localization["Inject.Failed"]);
        }
        finally
        {
            _activeInjection = null;
            cancellation.Dispose();
            Interlocked.Exchange(ref _injectionInProgress, 0);
        }
    }

    private void HandleResult(InjectionResult result, int skippedCharacters, double elapsedMs)
    {
        if (result.UipiBlocked)
        {
            _notification.Notify(_localization["AppTitle"], _localization["Inject.UipiBlocked"]);
            return;
        }

        if (result.Aborted)
        {
            FileLogger.Info($"Injection aborted after {result.KeystrokesSent} key events ({elapsedMs:F0}ms).");
            _notification.Notify(_localization["AppTitle"], _localization["Inject.Aborted"]);
            return;
        }

        FileLogger.Info(
            $"Injection complete via {_settings.InjectionMode}: {result.KeystrokesSent} key events, {skippedCharacters} chars skipped, {elapsedMs:F0}ms.");
        _notification.Notify(
            _localization["AppTitle"],
            string.Format(CultureInfo.CurrentCulture, _localization["Inject.Done"], result.KeystrokesSent, skippedCharacters));
    }
}
