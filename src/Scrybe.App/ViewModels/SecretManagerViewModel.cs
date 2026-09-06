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
    private bool _suppressPendingChanges;
    private bool _suppressDiscardPrompt;
    private bool _restoringRejectedSelection;
    private SecretEntry? _selectionBeforeChange;

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

    [ObservableProperty]
    private bool _hasPendingChanges;

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
        string? selectedId = SelectedSecret?.Id;
        Secrets.Clear();
        foreach (SecretEntry secret in _library.Secrets)
        {
            Secrets.Add(secret);
        }

        SelectedSecret = Secrets.FirstOrDefault(secret => string.Equals(secret.Id, selectedId, StringComparison.Ordinal))
            ?? Secrets.FirstOrDefault();
    }

    /// <summary>Updates the in-memory plaintext field from the WPF password box.</summary>
    /// <param name="value">The current password-box value.</param>
    public void SetSecretValue(string value) => SecretValue = value;

    partial void OnSelectedSecretChanging(SecretEntry? oldValue, SecretEntry? newValue)
    {
        _selectionBeforeChange = oldValue;
    }

    partial void OnSelectedSecretChanged(SecretEntry? value)
    {
        if (_restoringRejectedSelection)
        {
            return;
        }

        if (!_suppressDiscardPrompt
            && HasPendingChanges
            && !ReferenceEquals(value, _selectionBeforeChange)
            && !ConfirmDiscardChanges())
        {
            _restoringRejectedSelection = true;
            try
            {
                SelectedSecret = _selectionBeforeChange;
            }
            finally
            {
                _restoringRejectedSelection = false;
            }

            return;
        }

        if (value is null)
        {
            return;
        }

        LoadEditor(value);
    }

    partial void OnNameChanged(string value) => MarkPendingChanges();

    partial void OnUserNameChanged(string value) => MarkPendingChanges();

    partial void OnSecretValueChanged(string value) => MarkPendingChanges();

    [RelayCommand]
    private void New()
    {
        if (!_suppressDiscardPrompt && !ConfirmDiscardChanges())
        {
            return;
        }

        _suppressDiscardPrompt = true;
        _suppressPendingChanges = true;
        try
        {
            SelectedSecret = null;
            Name = string.Empty;
            UserName = string.Empty;
            SecretValue = string.Empty;
            StatusMessage = string.Empty;
            IsStatusError = false;
            HasPendingChanges = false;
            SecretPasswordResetRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _suppressPendingChanges = false;
            _suppressDiscardPrompt = false;
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (ReloadFromDiskCommand.IsRunning) { return; }
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

        SecretSaveResult result = await _library
            .SaveAsync(SelectedSecret?.Id, Name, UserName, SecretValue)
            .ConfigureAwait(true);
        if (!result.Persisted)
        {
            ShowSaveFailure();
            return;
        }
        SecretValue = string.Empty;
        SecretPasswordResetRequested?.Invoke(this, EventArgs.Empty);
        _suppressDiscardPrompt = true;
        try
        {
            Reload();
            SelectedSecret = Secrets.FirstOrDefault(secret => string.Equals(secret.Id, result.Entry.Id, StringComparison.Ordinal));
        }
        finally
        {
            _suppressDiscardPrompt = false;
        }

        StatusMessage = _localization["Secrets.Saved"];
        IsStatusError = false;
        HasPendingChanges = false;
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
        if (!_confirmation.ConfirmDanger(_localization["Secrets.DeleteConfirmTitle"], message, _localization["Dialog.Delete"]))
        {
            StatusMessage = string.Empty;
            IsStatusError = false;
            return;
        }

        bool persisted = await _library.DeleteAsync(SelectedSecret.Id).ConfigureAwait(true);
        _suppressDiscardPrompt = true;
        try
        {
            Reload();
            New();
        }
        finally
        {
            _suppressDiscardPrompt = false;
        }

        if (!persisted)
        {
            ShowSaveFailure();
            return;
        }

        StatusMessage = _localization["Secrets.Deleted"];
        IsStatusError = false;
    }

    private void ShowSaveFailure()
    {
        StatusMessage = _localization["Persist.SaveFailed"];
        IsStatusError = true;
        HasPendingChanges = true;
    }

    private void LoadEditor(SecretEntry value)
    {
        _suppressPendingChanges = true;
        try
        {
            Name = value.Name;
            UserName = value.UserName ?? string.Empty;
            SecretValue = string.Empty;
            StatusMessage = string.Empty;
            IsStatusError = false;
            HasPendingChanges = false;
            SecretPasswordResetRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _suppressPendingChanges = false;
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!HasPendingChanges)
        {
            return true;
        }

        return _confirmation.ConfirmDanger(
            _localization["Secrets.DiscardConfirmTitle"],
            _localization["Secrets.DiscardConfirmMessage"],
            _localization["Dialog.Discard"]);
    }

    private void MarkPendingChanges()
    {
        if (_suppressPendingChanges)
        {
            return;
        }

        HasPendingChanges = HasUnsavedChanges();
        if (HasPendingChanges && !IsStatusError)
        {
            StatusMessage = string.Empty;
        }
    }

    private bool HasUnsavedChanges()
    {
        if (SelectedSecret is null)
        {
            return !string.IsNullOrWhiteSpace(Name)
                || !string.IsNullOrWhiteSpace(UserName)
                || !string.IsNullOrEmpty(SecretValue);
        }

        return !Same(Name.Trim(), SelectedSecret.Name)
            || !Same(NormalizeOptional(UserName), SelectedSecret.UserName)
            || !string.IsNullOrEmpty(SecretValue);
    }

    private static string? NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool Same(string? left, string? right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}
