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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrybe.Core.Models;

namespace Scrybe.App.ViewModels;

public sealed partial class SettingsViewModel
{
    [ObservableProperty] private bool _reviewOcrBeforeCopy;
    [ObservableProperty] private InjectionProfileEditor? _selectedProfile;

    /// <summary>Profile drafts are applied only through the settings save action.</summary>
    public ObservableCollection<InjectionProfileEditor> Profiles { get; } = [];

    /// <summary>Available strategies for profile editing.</summary>
    public InjectionMode[] ProfileModes { get; } = Enum.GetValues<InjectionMode>();

    /// <summary>Requests the host's backup and restore browser.</summary>
    public event EventHandler? BackupsRequested;

    partial void OnReviewOcrBeforeCopyChanged(bool value) => MarkPendingChanges();

    private void InitializeFeatures()
    {
        ReviewOcrBeforeCopy = _settings.ReviewOcrBeforeCopy;
        foreach (InjectionProfile profile in _settings.InjectionProfiles ?? [])
        {
            if (profile is null || !profile.IsValid) { continue; }
            AddProfileRow(new()
            {
                ProcessName = profile.ProcessName,
                Mode = profile.Mode,
                KeyDelayMs = profile.KeyDelayMs.ToString(System.Globalization.CultureInfo.CurrentCulture),
                EnterExtraDelayMs = profile.EnterExtraDelayMs.ToString(System.Globalization.CultureInfo.CurrentCulture)
            });
        }
    }

    private void AddProfileRow(InjectionProfileEditor row)
    {
        row.PropertyChanged += OnProfileChanged;
        Profiles.Add(row);
    }

    private void OnProfileChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => MarkPendingChanges();

    [RelayCommand]
    private void AddProfile()
    {
        InjectionProfileEditor row = new();
        AddProfileRow(row);
        SelectedProfile = row;
        MarkPendingChanges();
    }

    [RelayCommand]
    private void RemoveProfile()
    {
        if (SelectedProfile is not InjectionProfileEditor row) { return; }
        row.PropertyChanged -= OnProfileChanged;
        Profiles.Remove(row);
        MarkPendingChanges();
    }

    [RelayCommand]
    private void OpenBackups() => BackupsRequested?.Invoke(this, EventArgs.Empty);

    private bool ValidateProfiles()
    {
        InjectionProfile[] profiles = Profiles.Select(row => row.Snapshot()).ToArray();
        if (profiles.Any(profile => !profile.IsValid)
            || profiles.Select(profile => profile.ProcessName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != profiles.Length)
        {
            StatusMessage = _localization["Profiles.Invalid"];
            IsStatusError = true;
            return false;
        }
        return true;
    }
}
