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
using Scrybe.Core.Logging;
using Scrybe.Core.Models;

namespace Scrybe.App.ViewModels;

/// <summary>View model for the managed capture-history tab.</summary>
public sealed partial class HistoryManagerViewModel : ObservableObject
{
    private readonly CaptureHistoryLibrary _library;
    private readonly IClipboardService _clipboard;
    private readonly INotificationService _notification;
    private readonly ILocalizationManager _localization;
    private readonly IConfirmationService _confirmation;

    [ObservableProperty]
    private HistoryPaletteListItem? _selectedEntry;

    [ObservableProperty]
    private string _selectedPreview = string.Empty;

    [ObservableProperty]
    private string _editText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _hasEntries;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private bool _canCopy;

    [ObservableProperty]
    private bool _canSaveEdit;

    [ObservableProperty]
    private bool _canReveal;

    [ObservableProperty]
    private bool _isTextRevealed;

    /// <summary>Initializes the history manager from the protected history library.</summary>
    /// <param name="library">The protected capture-history library.</param>
    /// <param name="clipboard">Clipboard service used for copied history text.</param>
    /// <param name="notification">Notification service for copy confirmations.</param>
    /// <param name="localization">Localization source.</param>
    /// <param name="confirmation">Confirmation service for destructive actions.</param>
    public HistoryManagerViewModel(
        CaptureHistoryLibrary library,
        IClipboardService clipboard,
        INotificationService notification,
        ILocalizationManager localization,
        IConfirmationService confirmation)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(confirmation);

        _library = library;
        _clipboard = clipboard;
        _notification = notification;
        _localization = localization;
        _confirmation = confirmation;
        Entries = [];
        Reload();
    }

    /// <summary>The managed history entries, newest first.</summary>
    public ObservableCollection<HistoryPaletteListItem> Entries { get; }

    /// <summary>Reloads the visible history entries from the current protected library state.</summary>
    public void Reload()
    {
        string? selectedId = SelectedEntry?.Id;
        Entries.Clear();

        foreach (CaptureHistoryEntry entry in _library.Entries.OrderByDescending(entry => entry.CapturedAtUtc))
        {
            Entries.Add(HistoryPaletteListItem.FromMetadata(
                entry,
                _localization["History.ProtectedPreview"],
                _localization["History.CharCount"]));
        }

        SelectedEntry = Entries.FirstOrDefault(entry => string.Equals(entry.Id, selectedId, StringComparison.Ordinal))
            ?? Entries.FirstOrDefault();
        UpdateEntryState();
    }

    partial void OnSelectedEntryChanged(HistoryPaletteListItem? value)
    {
        CanCopy = value is not null;
        CanReveal = value is not null;
        ResetSelectedText(value);
        UpdateEditState();
    }

    partial void OnEditTextChanged(string value)
    {
        UpdateEditState();
    }

    [RelayCommand]
    private void Reveal()
    {
        if (RevealSelectedEntry())
        {
            StatusMessage = _localization["History.Revealed"];
            IsStatusError = false;
        }
    }

    [RelayCommand]
    private async Task Copy()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        try
        {
            string? text = _library.RevealText(SelectedEntry.Id);
            if (string.IsNullOrEmpty(text))
            {
                StatusMessage = _localization["History.Missing"];
                IsStatusError = true;
                FileLogger.Warn("Capture history manager copy requested for a missing or empty entry.");
                return;
            }

            await _clipboard.SetTextAsync(text).ConfigureAwait(true);
            string message = string.Format(
                CultureInfo.CurrentCulture,
                _localization["Notify.CopiedFromHistory"],
                text.Length);
            _notification.Notify(_localization["AppTitle"], message);
            StatusMessage = string.Format(CultureInfo.CurrentCulture, _localization["History.Copied"], text.Length);
            IsStatusError = false;
        }
        catch (Exception exception)
        {
            StatusMessage = _localization["History.CopyFailed"];
            IsStatusError = true;
            FileLogger.Error("Failed to copy capture history entry from manager.", exception);
        }
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        string message = string.Format(
            CultureInfo.CurrentCulture,
            _localization["History.DeleteConfirmMessage"],
            SelectedEntry.Timestamp);
        if (!_confirmation.ConfirmDanger(_localization["History.DeleteConfirmTitle"], message, _localization["Dialog.Delete"]))
        {
            StatusMessage = string.Empty;
            IsStatusError = false;
            FileLogger.Info("Capture history entry delete cancelled.");
            return;
        }

        try
        {
            bool persisted = await _library.DeleteAsync(SelectedEntry.Id).ConfigureAwait(true);
            Reload();
            if (!persisted)
            {
                ShowSaveFailure();
                FileLogger.Warn("Capture history entry delete was not persisted; the current library was retained.");
                return;
            }

            StatusMessage = _localization["History.Deleted"];
            IsStatusError = false;
            FileLogger.Info("Capture history entry deleted from manager.");
        }
        catch (Exception exception)
        {
            StatusMessage = _localization["History.DeleteFailed"];
            IsStatusError = true;
            FileLogger.Error("Failed to delete capture history entry from manager.", exception);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveEdit))]
    private async Task SaveEdit()
    {
        if (ReloadFromDiskCommand.IsRunning) { return; }
        if (SelectedEntry is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditText))
        {
            StatusMessage = _localization["History.EmptyTextError"];
            IsStatusError = true;
            UpdateEditState();
            return;
        }

        string selectedId = SelectedEntry.Id;
        try
        {
            if (!IsTextRevealed)
            {
                StatusMessage = _localization["History.RevealFirst"];
                IsStatusError = true;
                return;
            }

            bool persisted = await _library.UpdateAsync(selectedId, EditText).ConfigureAwait(true);
            if (!persisted) { ShowSaveFailure(); return; }
            Reload();
            SelectedEntry = Entries.FirstOrDefault(entry => string.Equals(entry.Id, selectedId, StringComparison.Ordinal))
                ?? SelectedEntry;
            RevealSelectedEntry(showFailure: false);
            if (!persisted)
            {
                ShowSaveFailure();
                FileLogger.Warn("Capture history entry update was not persisted; the current library was retained.");
                return;
            }

            StatusMessage = _localization["History.Saved"];
            IsStatusError = false;
            FileLogger.Info("Capture history entry updated from manager.");
        }
        catch (Exception exception)
        {
            StatusMessage = _localization["History.SaveFailed"];
            IsStatusError = true;
            FileLogger.Error("Failed to save capture history entry from manager.", exception);
        }
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        if (!HasEntries)
        {
            return;
        }

        if (!_confirmation.ConfirmDanger(
            _localization["History.ClearConfirmTitle"],
            _localization["History.ClearConfirmMessage"],
            _localization["Dialog.ClearAll"]))
        {
            StatusMessage = string.Empty;
            IsStatusError = false;
            FileLogger.Info("Capture history manager clear-all cancelled.");
            return;
        }

        try
        {
            bool persisted = await _library.ClearAsync().ConfigureAwait(true);
            Reload();
            if (!persisted)
            {
                ShowSaveFailure();
                FileLogger.Warn("Capture history clear-all was not persisted; the current library was retained.");
                return;
            }

            StatusMessage = _localization["History.Cleared"];
            IsStatusError = false;
            FileLogger.Info("Capture history cleared from manager.");
        }
        catch (Exception exception)
        {
            StatusMessage = _localization["History.ClearFailed"];
            IsStatusError = true;
            FileLogger.Error("Failed to clear capture history from manager.", exception);
        }
    }

    private void UpdateEntryState()
    {
        HasEntries = Entries.Count > 0;
        IsEmpty = !HasEntries;
        CanCopy = SelectedEntry is not null;
        CanReveal = SelectedEntry is not null;
        ResetSelectedText(SelectedEntry);
        UpdateEditState();
    }

    private void ResetSelectedText(HistoryPaletteListItem? entry)
    {
        IsTextRevealed = false;
        if (entry is null)
        {
            SelectedPreview = string.Empty;
            EditText = string.Empty;
            return;
        }

        SelectedPreview = entry.Preview;
        EditText = string.Empty;
    }

    private bool RevealSelectedEntry(bool showFailure = true)
    {
        if (SelectedEntry is null)
        {
            return false;
        }

        try
        {
            string? text = _library.RevealText(SelectedEntry.Id);
            if (string.IsNullOrEmpty(text))
            {
                if (showFailure)
                {
                    StatusMessage = _localization["History.Missing"];
                    IsStatusError = true;
                }

                FileLogger.Warn("Capture history reveal requested for a missing or empty entry.");
                return false;
            }

            SelectedPreview = text;
            EditText = text;
            IsTextRevealed = true;
            UpdateEditState();
            return true;
        }
        catch (Exception exception)
        {
            SelectedPreview = SelectedEntry.Preview;
            EditText = string.Empty;
            IsTextRevealed = false;
            if (showFailure)
            {
                StatusMessage = _localization["History.RevealFailed"];
                IsStatusError = true;
            }

            FileLogger.Error($"Failed to reveal capture history entry '{SelectedEntry.Id}'.", exception);
            UpdateEditState();
            return false;
        }
    }

    private void UpdateEditState()
    {
        CanSaveEdit = SelectedEntry is not null && IsTextRevealed && !string.IsNullOrWhiteSpace(EditText);
        SaveEditCommand.NotifyCanExecuteChanged();
    }

    private void ShowSaveFailure()
    {
        string message = _localization["Persist.SaveFailed"];
        StatusMessage = message;
        IsStatusError = true;
        _notification.Notify(_localization["AppTitle"], message);
    }
}
