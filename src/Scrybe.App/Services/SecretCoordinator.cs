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

namespace Scrybe.App.Services;

/// <summary>Opens the secret palette/manager and routes selected secrets to the injection engine.</summary>
public sealed class SecretCoordinator
{
    private readonly SecretLibrary _library;
    private readonly InjectionCoordinator _injection;
    private readonly InjectionTargetConfirmer _targetConfirmer;
    private readonly ILocalizationManager _localization;

    private SecretPaletteWindow? _palette;

    /// <summary>Initializes the coordinator.</summary>
    /// <param name="library">The protected secret library.</param>
    /// <param name="injection">The injection coordinator.</param>
    /// <param name="targetConfirmer">Shared native target confirmation gate.</param>
    /// <param name="localization">Localization source for secret injection confirmations.</param>
    public SecretCoordinator(
        SecretLibrary library,
        InjectionCoordinator injection,
        InjectionTargetConfirmer targetConfirmer,
        ILocalizationManager localization)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(injection);
        ArgumentNullException.ThrowIfNull(targetConfirmer);
        ArgumentNullException.ThrowIfNull(localization);
        _library = library;
        _injection = injection;
        _targetConfirmer = targetConfirmer;
        _localization = localization;
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

    private async Task ConfirmAndInjectSecretAsync(string secretId, IntPtr target)
    {
        if (!_targetConfirmer.TryConfirmAndRestore(
            target,
            _localization["Secrets.ConfirmTitle"],
            _localization["Secrets.ConfirmTarget"],
            _localization["Secrets.TargetUnavailable"],
            _localization["Secrets.UntitledTarget"], out IInjectionContext? context))
        {
            return;
        }

        FileLogger.Info("Secret injection requested after target confirmation.");
        await InjectSecretAsync(secretId, context).ConfigureAwait(false);
    }

    private async Task InjectSecretAsync(string secretId, IInjectionContext? context)
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

            await _injection.InjectSecretAsync(secret, context: context).ConfigureAwait(false);
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
}
