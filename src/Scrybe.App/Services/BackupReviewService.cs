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

using System.Globalization;
using System.IO;
using Scrybe.App.ViewModels;
using Scrybe.App.Views;
using Scrybe.Core;
using Scrybe.Core.Interfaces;
using Scrybe.Core.IO;

namespace Scrybe.App.Services;

/// <summary>Local version browser with metadata preview and explicit guarded restore.</summary>
public sealed class BackupReviewService(ILocalizationManager localization, IConfirmationService confirmation)
{
    /// <summary>Returns the restored store name, or null when nothing was changed.</summary>
    public async Task<string?> ShowAsync()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppConstants.AppName);
        string[] names = [AppConstants.SettingsFileName, AppConstants.SnippetsFileName,
            AppConstants.SecretsFileName, AppConstants.CaptureHistoryFileName];
        List<ReviewChoice> choices = [];
        foreach (string name in names)
        {
            StoreBackups store = new(Path.Combine(root, name));
            foreach (StoreBackup backup in await store.ListAsync())
            {
                choices.Add(new(string.Format(CultureInfo.CurrentCulture, localization["Backup.Item"],
                    name, backup.CreatedAtUtc.ToLocalTime(), backup.ByteCount), (store, backup)));
            }
        }
        DataReviewViewModel review = new(localization["Backup.Title"], localization["Backup.Hint"], choices, []);
        if (new DataReviewWindow(review).ShowDialog() != true
            || review.SelectedEntry?.Value is not ValueTuple<StoreBackups, StoreBackup> selected) { return null; }
        RestorePreview preview = await selected.Item1.PreviewAsync(selected.Item2.Id);
        string message = string.Format(CultureInfo.CurrentCulture, localization["Backup.Confirm"],
            preview.Backup.StoreName, preview.Backup.CreatedAtUtc.ToLocalTime(), preview.EntryCount, preview.Backup.ByteCount);
        if (!confirmation.ConfirmDanger(localization["Backup.Title"], message, localization["Backup.Restore"])) { return null; }
        await selected.Item1.RestoreAsync(preview);
        return preview.Backup.StoreName;
    }
}
