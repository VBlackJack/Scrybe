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

namespace Scrybe.App.ViewModels;

/// <summary>View model for selecting a capture-history entry and copying it back to the clipboard.</summary>
public sealed partial class HistoryPaletteViewModel : ObservableObject
{
    private readonly List<HistoryPaletteListItem> _allEntries;

    [ObservableProperty]
    private HistoryPaletteListItem? _selectedEntry;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedPreview = string.Empty;

    [ObservableProperty]
    private bool _canCopy;

    [ObservableProperty]
    private bool _hasEntries;

    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>Initializes the palette with the available capture-history entries.</summary>
    /// <param name="entries">The display entries, newest first.</param>
    public HistoryPaletteViewModel(IReadOnlyList<HistoryPaletteListItem> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _allEntries = [.. entries];
        Entries = [];
        ApplyFilter();
        UpdateEntryState();
    }

    /// <summary>Raised with the selected entry id when the user requests a clipboard copy.</summary>
    public event EventHandler<string>? CopyRequested;

    /// <summary>Raised when the user requests clearing all history entries.</summary>
    public event EventHandler? ClearAllRequested;

    /// <summary>Raised with the selected entry id when the user requests deletion.</summary>
    public event EventHandler<string>? DeleteRequested;

    /// <summary>The selectable history entries.</summary>
    public ObservableCollection<HistoryPaletteListItem> Entries { get; }

    /// <summary>Replaces the displayed entries after a history mutation.</summary>
    /// <param name="entries">The replacement display entries, newest first.</param>
    public void ReplaceEntries(IReadOnlyList<HistoryPaletteListItem> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _allEntries.Clear();
        _allEntries.AddRange(entries);
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        string query = SearchText.Trim();
        string? selectedId = SelectedEntry?.Id;
        Entries.Clear();

        foreach (HistoryPaletteListItem entry in _allEntries.Where(entry => Matches(entry, query)))
        {
            Entries.Add(entry);
        }

        UpdateEntryState();
        HistoryPaletteListItem? nextSelection = Entries.FirstOrDefault(entry => string.Equals(entry.Id, selectedId, StringComparison.Ordinal))
            ?? Entries.FirstOrDefault();
        if (ReferenceEquals(SelectedEntry, nextSelection))
        {
            SelectedPreview = nextSelection?.Preview ?? string.Empty;
            CanCopy = nextSelection is not null;
        }
        else
        {
            SelectedEntry = nextSelection;
        }
    }

    partial void OnSelectedEntryChanged(HistoryPaletteListItem? value)
    {
        CanCopy = value is not null;
        SelectedPreview = value?.Preview ?? string.Empty;
    }

    [RelayCommand]
    private void Copy()
    {
        if (SelectedEntry is not null)
        {
            CopyRequested?.Invoke(this, SelectedEntry.Id);
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (HasEntries)
        {
            ClearAllRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedEntry is not null)
        {
            DeleteRequested?.Invoke(this, SelectedEntry.Id);
        }
    }

    private void UpdateEntryState()
    {
        HasEntries = _allEntries.Count > 0;
        IsEmpty = Entries.Count == 0;
        CanCopy = SelectedEntry is not null;
        SelectedPreview = SelectedEntry?.Preview ?? string.Empty;
    }

    private static bool Matches(HistoryPaletteListItem entry, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Contains(entry.Preview, query)
            || Contains(entry.Timestamp, query)
            || Contains(entry.CharCount.ToString(CultureInfo.CurrentCulture), query);
    }

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true;
}
