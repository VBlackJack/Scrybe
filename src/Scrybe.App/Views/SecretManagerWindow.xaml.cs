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
using Scrybe.App.ViewModels;

namespace Scrybe.App.Views;

/// <summary>The secret management window. Code-behind is limited to PasswordBox hand-off and reset.</summary>
public partial class SecretManagerWindow : Window
{
    private readonly SecretManagerViewModel _viewModel;

    /// <summary>Initializes the management window bound to its view model.</summary>
    /// <param name="viewModel">The manager view model.</param>
    public SecretManagerWindow(SecretManagerViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.SecretPasswordResetRequested += OnSecretPasswordResetRequested;
        Closed += OnClosed;
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.SetSecretValue(SecretPasswordBox.Password);

    private void OnSecretPasswordResetRequested(object? sender, EventArgs e) => SecretPasswordBox.Password = string.Empty;

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.SecretPasswordResetRequested -= OnSecretPasswordResetRequested;
        SecretPasswordBox.Password = string.Empty;
    }
}
