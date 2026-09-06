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

using Microsoft.Extensions.DependencyInjection;
using Scrybe.App.Services;
using Scrybe.Core.Logging;

namespace Scrybe.App;

public partial class App
{
    private bool _backupReviewOpen;
    private async void OnBackupsRequested(object? sender, EventArgs args)
    {
        if (_backupReviewOpen) { return; }
        _backupReviewOpen = true;
        try
        {
            if (_serviceProvider is null) { return; }
            string? restored = await _serviceProvider.GetRequiredService<BackupReviewService>().ShowAsync();
            if (restored is null) { return; }
            bool snippets = await _serviceProvider.GetRequiredService<SnippetLibrary>().LoadAsync();
            bool secrets = await _serviceProvider.GetRequiredService<SecretLibrary>().LoadAsync();
            bool history = await _serviceProvider.GetRequiredService<CaptureHistoryLibrary>().LoadAsync();
            // Editors retain their drafts; reopening/reloading them is an explicit user action.
            _mainViewModel!.Settings.StatusMessage = _serviceProvider.GetRequiredService<Scrybe.Core.Interfaces.ILocalizationManager>()
                [snippets && secrets && history ? "Backup.Restored" : "Persist.SaveFailed"];
            _mainViewModel.Settings.IsStatusError = !(snippets && secrets && history);
        }
        catch (Exception exception)
        {
            FileLogger.Warn($"Backup operation failed ({exception.GetType().Name}).");
            if (_mainViewModel is not null)
            {
                _mainViewModel.Settings.StatusMessage = _serviceProvider!.GetRequiredService<Scrybe.Core.Interfaces.ILocalizationManager>()["Backup.Failed"];
                _mainViewModel.Settings.IsStatusError = true;
            }
        }
        finally { _backupReviewOpen = false; }
    }
}
