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
using WpfMessageBox = System.Windows.MessageBox;

namespace Scrybe.App.Services;

/// <summary>Native OS MessageBox prompt used for password-grade target confirmation.</summary>
public sealed class NativeTargetConfirmationPrompt : ITargetConfirmationPrompt
{
    private readonly ILocalizationManager _localization;

    /// <summary>Initializes the prompt with localized action labels.</summary>
    /// <param name="localization">Localization source.</param>
    public NativeTargetConfirmationPrompt(ILocalizationManager localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
    }

    /// <inheritdoc />
    public bool Confirm(string title, string message)
    {
        System.Windows.Application? application = System.Windows.Application.Current;
        if (application is null)
        {
            MessageBoxResult result = WpfMessageBox.Show(
                message,
                title,
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            return result == MessageBoxResult.OK;
        }

        if (application.Dispatcher.CheckAccess())
        {
            return ConfirmOnUiThread(title, message, application);
        }

        return application.Dispatcher.Invoke(() => ConfirmOnUiThread(title, message, application));
    }

    /// <inheritdoc />
    public void ShowTargetUnavailable(string title, string message)
    {
        WpfMessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private bool ConfirmOnUiThread(string title, string message, System.Windows.Application application)
    {
        ConfirmationDialog dialog = new(
            title,
            message,
            _localization["Dialog.Inject"],
            _localization["Dialog.Cancel"],
            isDanger: false);

        Window? owner = ResolveOwner(application);
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
