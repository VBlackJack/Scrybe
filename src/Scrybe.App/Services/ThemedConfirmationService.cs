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

using System.Windows;
using Scrybe.App.Views;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;

namespace Scrybe.App.Services;

/// <summary>Dracula-themed implementation for destructive confirmation prompts.</summary>
public sealed class ThemedConfirmationService : IConfirmationService
{
    private readonly ILocalizationManager _localization;

    /// <summary>Initializes the confirmation service with localized generic button labels.</summary>
    /// <param name="localization">Localization source.</param>
    public ThemedConfirmationService(ILocalizationManager localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
    }

    /// <inheritdoc />
    public bool ConfirmDanger(string title, string message)
        => ConfirmDanger(title, message, _localization["Dialog.Confirm"]);

    /// <inheritdoc />
    public bool ConfirmDanger(string title, string message, string confirmText)
    {
        System.Windows.Application? application = System.Windows.Application.Current;
        if (application is null)
        {
            FileLogger.Warn("Themed confirmation requested without a current WPF application.");
            return false;
        }

        if (application.Dispatcher.CheckAccess())
        {
            return ConfirmDangerOnUiThread(title, message, confirmText, application);
        }

        return application.Dispatcher.Invoke(() => ConfirmDangerOnUiThread(title, message, confirmText, application));
    }

    private bool ConfirmDangerOnUiThread(string title, string message, string confirmText, System.Windows.Application application)
    {
        Window? owner = ResolveOwner(application);
        ConfirmationDialog dialog = new(
            title,
            message,
            confirmText,
            _localization["Dialog.Cancel"]);

        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        bool? result = dialog.ShowDialog();
        return result == true;
    }

    private static Window? ResolveOwner(System.Windows.Application application)
    {
        foreach (Window window in application.Windows)
        {
            if (window.IsActive)
            {
                return window;
            }
        }

        Window? mainWindow = application.MainWindow;
        return mainWindow is { IsVisible: true } ? mainWindow : null;
    }
}
