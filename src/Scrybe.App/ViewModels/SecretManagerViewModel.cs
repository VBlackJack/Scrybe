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

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrybe.App.Services;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;

namespace Scrybe.App.ViewModels;

/// <summary>View model for creating, updating and deleting DPAPI-protected secrets.</summary>
public sealed partial class SecretManagerViewModel : ObservableObject
{
    private readonly SecretLibrary _library;
    private readonly ILocalizationManager _localization;
    private readonly IConfirmationService _confirmation;

    [ObservableProperty]
    private SecretEntry? _selectedSecret;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _secretValue = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    /// <summary>Initializes the manager from the protected secret library.</summary>
    /// <param name="library">The secret library.</param>
    /// <param name="localization">Localization source for status text.</param>
    /// <param name="confirmation">Confirmation service for destructive actions.</param>
    public SecretManagerViewModel(
        SecretLibrary library,
        ILocalizationManager localization,
        IConfirmationService confirmation)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(confirmation);
        _library = library;
        _localization = localization;
        _confirmation = confirmation;
        Secrets = new ObservableCollection<SecretEntry>(library.Secrets);
    }

    /// <summary>Raised when the password box should be cleared.</summary>
    public event EventHandler? SecretPasswordResetRequested;

    /// <summary>The protected secrets shown in the list.</summary>
    public ObservableCollection<SecretEntry> Secrets { get; }

    /// <summary>Reloads the visible secrets from the current library state.</summary>
    public void Reload()
    {
        Secrets.Clear();
        foreach (SecretEntry secret in _library.Secrets)
        {
            Secrets.Add(secret);
        }
    }

    /// <summary>Updates the in-memory plaintext field from the WPF password box.</summary>
    /// <param name="value">The current password-box value.</param>
    public void SetSecretValue(string value) => SecretValue = value;

    partial void OnSelectedSecretChanged(SecretEntry? value)
    {
        if (value is null)
        {
            return;
        }

        Name = value.Name;
        UserName = value.UserName ?? string.Empty;
        SecretValue = string.Empty;
        StatusMessage = string.Empty;
        IsStatusError = false;
        SecretPasswordResetRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void New()
    {
        SelectedSecret = null;
        Name = string.Empty;
        UserName = string.Empty;
        SecretValue = string.Empty;
        StatusMessage = string.Empty;
        IsStatusError = false;
        SecretPasswordResetRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = _localization["Secrets.NameRequired"];
            IsStatusError = true;
            return;
        }

        if (SelectedSecret is null && string.IsNullOrEmpty(SecretValue))
        {
            StatusMessage = _localization["Secrets.PasswordRequired"];
            IsStatusError = true;
            return;
        }

        SecretEntry saved = await _library
            .SaveAsync(SelectedSecret?.Id, Name, UserName, SecretValue)
            .ConfigureAwait(true);
        SecretValue = string.Empty;
        SecretPasswordResetRequested?.Invoke(this, EventArgs.Empty);
        Reload();
        SelectedSecret = Secrets.FirstOrDefault(secret => string.Equals(secret.Id, saved.Id, StringComparison.Ordinal));
        StatusMessage = _localization["Secrets.Saved"];
        IsStatusError = false;
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedSecret is null)
        {
            return;
        }

        string secretName = SelectedSecret.Name;
        string message = string.Format(
            CultureInfo.CurrentCulture,
            _localization["Secrets.DeleteConfirmMessage"],
            secretName);
        if (!_confirmation.ConfirmDanger(_localization["Secrets.DeleteConfirmTitle"], message))
        {
            StatusMessage = string.Empty;
            IsStatusError = false;
            return;
        }

        await _library.DeleteAsync(SelectedSecret.Id).ConfigureAwait(true);
        Reload();
        New();
        StatusMessage = _localization["Secrets.Deleted"];
        IsStatusError = false;
    }
}
