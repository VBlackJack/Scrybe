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
using WpfMessageBox = System.Windows.MessageBox;

namespace Scrybe.App.Services;

/// <summary>Native OS MessageBox prompt used for password-grade target confirmation.</summary>
public sealed class NativeTargetConfirmationPrompt : ITargetConfirmationPrompt
{
    /// <inheritdoc />
    public bool Confirm(string title, string message)
    {
        MessageBoxResult result = WpfMessageBox.Show(
            message,
            title,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.OK;
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
}
