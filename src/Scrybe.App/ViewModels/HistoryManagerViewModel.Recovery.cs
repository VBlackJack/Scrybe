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

namespace Scrybe.App.ViewModels;

public sealed partial class HistoryManagerViewModel
{
    /// <summary>Reloads current metadata and restores the current in-memory editor.</summary>
    [RelayCommand]
    private async Task ReloadFromDisk()
    {
        if (SaveEditCommand.IsRunning) { return; }
        string? id = SelectedEntry?.Id;
        string draft = EditText;
        bool revealed = IsTextRevealed;
        if (!await _library.LoadAsync().ConfigureAwait(true)) { ShowSaveFailure(); return; }
        Reload();
        SelectedEntry = Entries.FirstOrDefault(item => item.Id == id);
        IsTextRevealed = revealed;
        EditText = draft;
        UpdateEditState();
        IsStatusError = false;
        StatusMessage = _localization["Persist.ReloadedDraft"];
    }
}
