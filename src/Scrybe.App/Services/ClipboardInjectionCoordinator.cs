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

using Scrybe.App.Interop;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;
using Scrybe.Core.Security;

namespace Scrybe.App.Services;

/// <summary>Injects the current clipboard text as password-grade keystrokes after target confirmation.</summary>
public sealed class ClipboardInjectionCoordinator
{
    private readonly IClipboardService _clipboard;
    private readonly InjectionCoordinator _injection;
    private readonly IInjectionTargetConfirmer _targetConfirmer;
    private readonly INotificationService _notification;
    private readonly ILocalizationManager _localization;
    private readonly AppSettings _settings;

    /// <summary>Initializes the clipboard injection coordinator.</summary>
    /// <param name="clipboard">Clipboard service used to read and optionally clear clipboard text.</param>
    /// <param name="injection">No-echo injection coordinator.</param>
    /// <param name="targetConfirmer">Shared native target confirmation gate.</param>
    /// <param name="notification">Notification service for no-op and completion messages.</param>
    /// <param name="localization">Localization source.</param>
    /// <param name="settings">Live settings.</param>
    public ClipboardInjectionCoordinator(
        IClipboardService clipboard,
        InjectionCoordinator injection,
        IInjectionTargetConfirmer targetConfirmer,
        INotificationService notification,
        ILocalizationManager localization,
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(injection);
        ArgumentNullException.ThrowIfNull(targetConfirmer);
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(settings);

        _clipboard = clipboard;
        _injection = injection;
        _targetConfirmer = targetConfirmer;
        _notification = notification;
        _localization = localization;
        _settings = settings;
    }

    /// <summary>Reads clipboard text, confirms the target, then injects it through the secret path.</summary>
    public async Task InjectClipboardAsync()
    {
        IntPtr target = InjectionInterop.GetForegroundWindowHandle();

        try
        {
            string? text = await _clipboard.GetTextAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(text))
            {
                FileLogger.Info("Clipboard injection requested but clipboard text is empty or unavailable.");
                _notification.Notify(_localization["AppTitle"], _localization["Notify.ClipboardEmpty"]);
                return;
            }

            if (!_targetConfirmer.TryConfirmAndRestore(
                target,
                _localization["Inject.ConfirmTitle"],
                _localization["Inject.ConfirmClipboardTarget"],
                _localization["Inject.TargetUnavailable"],
                _localization["Inject.UntitledTarget"]))
            {
                return;
            }

            char[] chars = text.ToCharArray();
            InjectionResult result;
            try
            {
                FileLogger.Info("Clipboard injection requested after target confirmation.");
                result = await _injection.InjectSecretAsync(chars, notifySuccess: false).ConfigureAwait(false);
            }
            finally
            {
                SecretMemory.Clear(chars);
            }

            if (!result.Success)
            {
                FileLogger.Info("Clipboard injection did not complete; clipboard retained.");
                return;
            }

            if (_settings.ClearClipboardAfterInjection)
            {
                await _clipboard.SetTextAsync(string.Empty).ConfigureAwait(true);
                FileLogger.Info("Clipboard cleared after clipboard injection.");
            }

            _notification.Notify(_localization["AppTitle"], _localization["Notify.ClipboardInjected"]);
            FileLogger.Info("Clipboard injection flow completed.");
        }
        catch (Exception exception)
        {
            FileLogger.Error("Clipboard injection failed.", exception);
            _notification.Notify(_localization["AppTitle"], _localization["Inject.Failed"]);
        }
    }
}
