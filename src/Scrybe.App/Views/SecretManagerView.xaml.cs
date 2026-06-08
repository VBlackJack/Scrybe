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

/// <summary>Secret manager tab content. Code-behind is limited to PasswordBox hand-off and reset.</summary>
public sealed partial class SecretManagerView : System.Windows.Controls.UserControl
{
    private SecretManagerViewModel? _viewModel;

    /// <summary>Initializes the secret manager view.</summary>
    public SecretManagerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel?.SetSecretValue(SecretPasswordBox.Password);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.SecretPasswordResetRequested -= OnSecretPasswordResetRequested;
        }

        _viewModel = e.NewValue as SecretManagerViewModel;
        if (_viewModel is not null)
        {
            _viewModel.SecretPasswordResetRequested += OnSecretPasswordResetRequested;
        }
    }

    private void OnSecretPasswordResetRequested(object? sender, EventArgs e)
    {
        SecretPasswordBox.Password = string.Empty;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SecretPasswordBox.Password = string.Empty;
    }
}
