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
using System.Windows;
using Scrybe.App.Interop;
using Scrybe.Core.Logging;
using WpfMessageBox = System.Windows.MessageBox;

namespace Scrybe.App.Services;

/// <summary>Shared native target-confirmation gate for password-grade injection flows.</summary>
public sealed class InjectionTargetConfirmer : IInjectionTargetConfirmer
{
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
        string untitledTargetText)
    {
        if (!TryConfirmTarget(target, confirmTitle, confirmMessageTemplate, targetUnavailableMessage, untitledTargetText))
        {
            return false;
        }

        if (!InjectionInterop.TryGetTargetInfo(target, out _))
        {
            ShowTargetUnavailable(confirmTitle, targetUnavailableMessage);
            return false;
        }

        if (!InjectionInterop.TryRestoreForeground(target))
        {
            FileLogger.Warn("Injection target restore failed; injection aborted.");
            ShowTargetUnavailable(confirmTitle, targetUnavailableMessage);
            return false;
        }

        return true;
    }

    private static bool TryConfirmTarget(
        IntPtr target,
        string confirmTitle,
        string confirmMessageTemplate,
        string targetUnavailableMessage,
        string untitledTargetText)
    {
        if (!InjectionInterop.TryGetTargetInfo(target, out InjectionInterop.TargetInfo? targetInfo) || targetInfo is null)
        {
            ShowTargetUnavailable(confirmTitle, targetUnavailableMessage);
            return false;
        }

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

        MessageBoxResult result = WpfMessageBox.Show(
            message,
            confirmTitle,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            FileLogger.Info("Injection target confirmation cancelled.");
            return false;
        }

        return true;
    }

    private static void ShowTargetUnavailable(string confirmTitle, string targetUnavailableMessage)
    {
        WpfMessageBox.Show(
            targetUnavailableMessage,
            confirmTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
