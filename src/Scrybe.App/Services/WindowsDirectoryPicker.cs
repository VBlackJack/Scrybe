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

using System.IO;
using Forms = System.Windows.Forms;

namespace Scrybe.App.Services;

/// <summary>Windows Forms-backed folder picker for WPF settings.</summary>
public sealed class WindowsDirectoryPicker : IDirectoryPicker
{
    /// <inheritdoc />
    public bool TryPickDirectory(string? initialDirectory, out string directory)
    {
        directory = string.Empty;
        using Forms.FolderBrowserDialog dialog = new()
        {
            Description = "Select the Scrybe captures folder",
            UseDescriptionForTitle = true,
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.SelectedPath = initialDirectory;
        }

        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return false;
        }

        directory = dialog.SelectedPath;
        return true;
    }
}
