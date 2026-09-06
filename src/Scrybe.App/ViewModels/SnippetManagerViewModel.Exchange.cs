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
using Scrybe.Core.Logging;

namespace Scrybe.App.ViewModels;

public sealed partial class SnippetManagerViewModel
{
    [RelayCommand]
    private async Task ExportSnippets()
    {
        if (_exchange is null) { return; }
        try
        {
            if (await _exchange.ExportAsync(_library)) { StatusMessage = _localization["Exchange.Exported"]; IsStatusError = false; }
        }
        catch (Exception exception) { ShowExchangeFailure(exception); }
    }

    [RelayCommand]
    private async Task ImportSnippets()
    {
        if (_exchange is null || SaveCommand.IsRunning || ReloadFromDiskCommand.IsRunning) { return; }
        if (HasPendingChanges)
        {
            StatusMessage = _localization["Exchange.SaveDraftFirst"]; IsStatusError = true; return;
        }
        try
        {
            if (await _exchange.ImportAsync(_library))
            {
                Reload();
                StatusMessage = _localization["Exchange.Imported"]; IsStatusError = false;
            }
        }
        catch (Exception exception) { ShowExchangeFailure(exception); }
    }

    private void ShowExchangeFailure(Exception exception)
    {
        // Payload-bearing deserialization errors must not disclose snippet text in logs.
        FileLogger.Warn($"Snippet exchange failed ({exception.GetType().Name}).");
        StatusMessage = _localization["Exchange.Failed"]; IsStatusError = true;
    }
}
