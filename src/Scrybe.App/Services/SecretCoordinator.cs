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
using Scrybe.App.ViewModels;
using Scrybe.App.Views;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;
using Scrybe.Core.Security;
using System.Globalization;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;

namespace Scrybe.App.Services;

/// <summary>Opens the secret palette/manager and routes selected secrets to the injection engine.</summary>
public sealed class SecretCoordinator
{
    private readonly SecretLibrary _library;
    private readonly InjectionCoordinator _injection;
    private readonly ILocalizationManager _localization;
    private readonly IConfirmationService _confirmation;

    private SecretPaletteWindow? _palette;
    private SecretManagerWindow? _manager;

    /// <summary>Initializes the coordinator.</summary>
    /// <param name="library">The protected secret library.</param>
    /// <param name="injection">The injection coordinator.</param>
    /// <param name="localization">Localization source for the manager view model.</param>
    /// <param name="confirmation">Confirmation service for destructive actions.</param>
    public SecretCoordinator(
        SecretLibrary library,
        InjectionCoordinator injection,
        ILocalizationManager localization,
        IConfirmationService confirmation)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(injection);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(confirmation);
        _library = library;
        _injection = injection;
        _localization = localization;
        _confirmation = confirmation;
    }

    /// <summary>Opens (or focuses) the secret palette, remembering the current foreground target.</summary>
    public void ShowPalette()
    {
        if (_palette is not null)
        {
            _palette.Activate();
            return;
        }

        IntPtr target = InjectionInterop.GetForegroundWindowHandle();
        SecretPaletteViewModel viewModel = new(_library.Secrets);
        SecretPaletteWindow window = new(viewModel);

        viewModel.InjectRequested += (_, secretId) =>
        {
            window.Close();
            _ = ConfirmAndInjectSecretAsync(secretId, target);
        };
        window.Closed += (_, _) => _palette = null;

        _palette = window;
        window.Show();
        window.Activate();
    }

    /// <summary>Opens (or focuses) the secret management window.</summary>
    public void ShowManager()
    {
        if (_manager is not null)
        {
            _manager.Activate();
            return;
        }

        SecretManagerViewModel viewModel = new(_library, _localization, _confirmation);
        SecretManagerWindow window = new(viewModel);
        window.Closed += (_, _) => _manager = null;

        _manager = window;
        window.Show();
        window.Activate();
    }

    private async Task ConfirmAndInjectSecretAsync(string secretId, IntPtr target)
    {
        if (!TryConfirmTarget(target))
        {
            return;
        }

        if (!InjectionInterop.TryGetTargetInfo(target, out _))
        {
            ShowTargetUnavailable();
            return;
        }

        if (!InjectionInterop.TryRestoreForeground(target))
        {
            FileLogger.Warn("Secret target restore failed; injection aborted.");
            ShowTargetUnavailable();
            return;
        }

        FileLogger.Info("Secret injection requested after target confirmation.");
        await InjectSecretAsync(secretId).ConfigureAwait(false);
    }

    private async Task InjectSecretAsync(string secretId)
    {
        char[]? secret = null;
        try
        {
            secret = _library.RevealSecret(secretId);
            if (secret is null || secret.Length == 0)
            {
                FileLogger.Warn("Secret injection requested for a missing or empty secret.");
                return;
            }

            await _injection.InjectSecretAsync(secret).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            FileLogger.Error("Secret injection failed.", exception);
        }
        finally
        {
            SecretMemory.Clear(secret);
        }
    }

    private bool TryConfirmTarget(IntPtr target)
    {
        if (!InjectionInterop.TryGetTargetInfo(target, out InjectionInterop.TargetInfo? targetInfo) || targetInfo is null)
        {
            ShowTargetUnavailable();
            return false;
        }

        string title = string.IsNullOrWhiteSpace(targetInfo.Title)
            ? _localization["Secrets.UntitledTarget"]
            : targetInfo.Title;
        string handle = "0x" + targetInfo.Hwnd.ToInt64().ToString("X", CultureInfo.InvariantCulture);
        string message = string.Format(
            CultureInfo.CurrentCulture,
            _localization["Secrets.ConfirmTarget"],
            title,
            targetInfo.ProcessName,
            targetInfo.ProcessId,
            handle);

        MessageBoxResult result = WpfMessageBox.Show(
            message,
            _localization["Secrets.ConfirmTitle"],
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            FileLogger.Info("Secret target confirmation cancelled.");
            return false;
        }

        return true;
    }

    private void ShowTargetUnavailable()
    {
        WpfMessageBox.Show(
            _localization["Secrets.TargetUnavailable"],
            _localization["Secrets.ConfirmTitle"],
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
