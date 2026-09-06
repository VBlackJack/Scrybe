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

using CommunityToolkit.Mvvm.Input;
using Scrybe.Core.Interfaces;

namespace Scrybe.App.ViewModels;

public sealed partial class SettingsViewModel
{
    /// <summary>Refreshes the disk revision while preserving the current settings form for review.</summary>
    [RelayCommand]
    private async Task ReloadFromDisk()
    {
        if (SaveCommand.IsRunning) { return; }
        await _store.LoadAsync().ConfigureAwait(true);
        if (_store is IStoreReadState { CanSave: false }) { ShowSaveFailure(); return; }
        HasPendingChanges = true;
        IsStatusError = false;
        StatusMessage = _localization["Persist.ReloadedSettings"];
    }
}
