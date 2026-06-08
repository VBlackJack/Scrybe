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
            try
            {
                string? text = _library.RevealText(entry.Id);
                if (text is not null)
                {
                    Entries.Add(HistoryPaletteListItem.FromEntry(entry, text));
                }
            }
            catch (Exception exception)
            {
                FileLogger.Error($"Failed to reveal capture history entry '{entry.Id}'.", exception);
            }
        }

        SelectedEntry = Entries.FirstOrDefault(entry => string.Equals(entry.Id, selectedId, StringComparison.Ordinal))
            ?? Entries.FirstOrDefault();
        UpdateEntryState();
    }

    partial void OnSelectedEntryChanged(HistoryPaletteListItem? value)
    {
        CanCopy = value is not null;
        UpdateSelectedText(value);
        UpdateEditState();
    }

    partial void OnEditTextChanged(string value)
    {
        UpdateEditState();
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
        if (!_confirmation.ConfirmDanger(_localization["History.DeleteConfirmTitle"], message))
        {
            StatusMessage = string.Empty;
            IsStatusError = false;
            FileLogger.Info("Capture history entry delete cancelled.");
            return;
        }

        try
        {
            await _library.DeleteAsync(SelectedEntry.Id).ConfigureAwait(true);
            Reload();
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
            await _library.UpdateAsync(selectedId, EditText).ConfigureAwait(true);
            Reload();
            SelectedEntry = Entries.FirstOrDefault(entry => string.Equals(entry.Id, selectedId, StringComparison.Ordinal))
                ?? SelectedEntry;
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
            _localization["History.ClearConfirmMessage"]))
        {
            StatusMessage = string.Empty;
            IsStatusError = false;
            FileLogger.Info("Capture history manager clear-all cancelled.");
            return;
        }

        try
        {
            await _library.ClearAsync().ConfigureAwait(true);
            Reload();
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
        UpdateSelectedText(SelectedEntry);
        UpdateEditState();
    }

    private void UpdateSelectedText(HistoryPaletteListItem? entry)
    {
        if (entry is null)
        {
            SelectedPreview = string.Empty;
            EditText = string.Empty;
            return;
        }

        try
        {
            string text = _library.RevealText(entry.Id) ?? entry.Preview;
            SelectedPreview = text;
            EditText = text;
        }
        catch (Exception exception)
        {
            SelectedPreview = entry.Preview;
            EditText = entry.Preview;
            FileLogger.Error($"Failed to reveal capture history preview for '{entry.Id}'.", exception);
        }
    }

    private void UpdateEditState()
    {
        CanSaveEdit = SelectedEntry is not null && !string.IsNullOrWhiteSpace(EditText);
        SaveEditCommand.NotifyCanExecuteChanged();
    }
}
