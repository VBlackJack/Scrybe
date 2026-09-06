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
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;

namespace Scrybe.App.Services;

/// <summary>Shared native target-confirmation gate for password-grade injection flows.</summary>
public sealed class InjectionTargetConfirmer : IInjectionTargetConfirmer
{
    private readonly ITargetWindowGateway _targetGateway;
    private readonly ITargetConfirmationPrompt _prompt;
    private readonly AppSettings? _settings;
    private readonly ILocalizationManager? _localization;

    /// <summary>Initializes the target confirmer with testable OS-bound seams.</summary>
    /// <param name="targetGateway">Gateway for target-window metadata and foreground restore.</param>
    /// <param name="prompt">Native confirmation prompt abstraction.</param>
    public InjectionTargetConfirmer(ITargetWindowGateway targetGateway, ITargetConfirmationPrompt prompt,
        AppSettings? settings = null, ILocalizationManager? localization = null)
    {
        ArgumentNullException.ThrowIfNull(targetGateway);
        ArgumentNullException.ThrowIfNull(prompt);

        _targetGateway = targetGateway;
        _prompt = prompt;
        _settings = settings;
        _localization = localization;
    }

    /// <summary>
    /// Confirms the captured target with a native OS dialog and restores focus before injection.
    /// </summary>
    /// <param name="target">The window captured before any Scrybe UI was shown.</param>
    /// <param name="confirmTitle">Dialog title.</param>
    /// <param name="confirmMessageTemplate">Message template receiving title, process name, pid and hwnd.</param>
    /// <param name="targetUnavailableMessage">Message shown when the target is unavailable.</param>
    /// <param name="untitledTargetText">Fallback title for untitled windows.</param>
    /// <returns><see langword="true"/> when the user confirmed and foreground restore succeeded.</returns>
    public bool TryConfirmAndRestore(
        IntPtr target,
        string confirmTitle,
        string confirmMessageTemplate,
        string targetUnavailableMessage,
        string untitledTargetText, out IInjectionContext? context)
    {
        context = null;
        if (!_targetGateway.TryGetInfo(target, out TargetWindowInfo? targetInfo) || targetInfo is null)
        {
            _prompt.ShowTargetUnavailable(confirmTitle, targetUnavailableMessage);
            return false;
        }

        InjectionProfile? profile = _settings is null ? null : InjectionProfile.Resolve(_settings, targetInfo.ProcessName);
        if (!ConfirmTarget(targetInfo, confirmTitle, confirmMessageTemplate, untitledTargetText, profile))
        {
            return false;
        }

        if (!_targetGateway.TryGetInfo(target, out TargetWindowInfo? currentTarget) || currentTarget is null)
        {
            _prompt.ShowTargetUnavailable(confirmTitle, targetUnavailableMessage);
            return false;
        }

        if (!IsSameTarget(targetInfo, currentTarget))
        {
            FileLogger.Warn("Injection target changed after confirmation; injection aborted.");
            _prompt.ShowTargetUnavailable(confirmTitle, targetUnavailableMessage);
            return false;
        }

        if (!_targetGateway.TryRestore(target))
        {
            FileLogger.Warn("Injection target restore failed; injection aborted.");
            _prompt.ShowTargetUnavailable(confirmTitle, targetUnavailableMessage);
            return false;
        }

        IntPtr foreground = _targetGateway.GetForegroundWindow();
        if (foreground != targetInfo.Hwnd)
        {
            FileLogger.Warn("Injection target restore did not regain foreground focus; injection aborted.");
            _prompt.ShowTargetUnavailable(confirmTitle, targetUnavailableMessage);
            return false;
        }

        context = new ConfirmedInjectionContext(_targetGateway, targetInfo, profile);
        return true;
    }

    private static bool IsSameTarget(TargetWindowInfo confirmed, TargetWindowInfo current)
        => confirmed.Hwnd == current.Hwnd
        && confirmed.ProcessId == current.ProcessId
        && string.Equals(confirmed.ProcessName, current.ProcessName, StringComparison.Ordinal);

    private bool ConfirmTarget(
        TargetWindowInfo targetInfo,
        string confirmTitle,
        string confirmMessageTemplate,
        string untitledTargetText, InjectionProfile? profile)
    {
        string title = string.IsNullOrWhiteSpace(targetInfo.Title)
            ? untitledTargetText
            : targetInfo.Title;
        string handle = "0x" + targetInfo.Hwnd.ToInt64().ToString("X", CultureInfo.InvariantCulture);
        string message = string.Format(
            CultureInfo.CurrentCulture,
            confirmMessageTemplate,
            title,
            targetInfo.ProcessName,
            targetInfo.ProcessId,
            handle);

        if (profile is not null && _localization is not null)
        {
            message += Environment.NewLine + string.Format(CultureInfo.CurrentCulture,
                _localization["Profiles.Confirmation"], profile.Mode, profile.KeyDelayMs, profile.EnterExtraDelayMs);
        }
        if (!_prompt.Confirm(confirmTitle, message))
        {
            FileLogger.Info("Injection target confirmation cancelled.");
            return false;
        }

        return true;
    }
}
