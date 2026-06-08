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
using Scrybe.App.ViewModels;
using Scrybe.App.Views;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;

namespace Scrybe.App.Services;

/// <summary>Opens the capture-history palette and routes selected entries back to the clipboard.</summary>
public sealed class CaptureHistoryCoordinator
{
    private readonly CaptureHistoryLibrary _library;
    private readonly IClipboardService _clipboard;
    private readonly INotificationService _notification;
    private readonly ILocalizationManager _localization;
    private readonly IConfirmationService _confirmation;

    private HistoryPaletteWindow? _palette;

    /// <summary>Initializes the coordinator.</summary>
    /// <param name="library">The protected capture-history library.</param>
    /// <param name="clipboard">Clipboard service used for selected history text.</param>
    /// <param name="notification">Notification service for copy confirmation.</param>
    /// <param name="localization">Localization source.</param>
    /// <param name="confirmation">Confirmation service for destructive actions.</param>
    public CaptureHistoryCoordinator(
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
    }

    /// <summary>Opens or focuses the capture-history palette.</summary>
    public void ShowPalette()
    {
        if (_palette is not null)
        {
            _palette.Activate();
            return;
        }

        HistoryPaletteViewModel viewModel = new(BuildItems());
        HistoryPaletteWindow window = new(viewModel);

        viewModel.CopyRequested += (_, entryId) =>
        {
            window.Close();
            _ = CopyToClipboardAsync(entryId);
        };
        viewModel.ClearAllRequested += (_, _) => _ = ClearAllFromPaletteAsync(window);
        viewModel.DeleteRequested += (_, entryId) => _ = DeleteFromPaletteAsync(viewModel, entryId);
        window.Closed += (_, _) => _palette = null;

        _palette = window;
        window.Show();
        window.Activate();
    }

    /// <summary>Confirms and clears all history entries.</summary>
    /// <returns><see langword="true"/> when history was cleared.</returns>
    public async Task<bool> ClearAllAsync()
    {
        bool confirmed = _confirmation.ConfirmDanger(
            _localization["History.ClearConfirmTitle"],
            _localization["History.ClearConfirmMessage"]);

        if (!confirmed)
        {
            FileLogger.Info("Capture history clear-all cancelled.");
            return false;
        }

        await _library.ClearAsync().ConfigureAwait(false);
        FileLogger.Info("Capture history cleared.");
        return true;
    }

    private async Task CopyToClipboardAsync(string entryId)
    {
        try
        {
            string? text = _library.RevealText(entryId);
            if (string.IsNullOrEmpty(text))
            {
                FileLogger.Warn("Capture history copy requested for a missing or empty entry.");
                return;
            }

            await _clipboard.SetTextAsync(text).ConfigureAwait(false);
            string message = string.Format(
                CultureInfo.CurrentCulture,
                _localization["Notify.CopiedFromHistory"],
                text.Length);
            _notification.Notify(_localization["AppTitle"], message);
        }
        catch (Exception exception)
        {
            FileLogger.Error("Failed to copy capture history entry.", exception);
        }
    }

    private async Task ClearAllFromPaletteAsync(HistoryPaletteWindow window)
    {
        try
        {
            bool cleared = await ClearAllAsync().ConfigureAwait(true);
            if (cleared)
            {
                window.Close();
            }
        }
        catch (Exception exception)
        {
            FileLogger.Error("Failed to clear capture history.", exception);
        }
    }

    private async Task DeleteFromPaletteAsync(HistoryPaletteViewModel viewModel, string entryId)
    {
        try
        {
            await _library.DeleteAsync(entryId).ConfigureAwait(true);
            viewModel.ReplaceEntries(BuildItems());
        }
        catch (Exception exception)
        {
            FileLogger.Error("Failed to delete capture history entry.", exception);
        }
    }

    private IReadOnlyList<HistoryPaletteListItem> BuildItems()
    {
        List<HistoryPaletteListItem> items = [];
        foreach (CaptureHistoryEntry entry in _library.Entries)
        {
            try
            {
                string? text = _library.RevealText(entry.Id);
                if (text is not null)
                {
                    items.Add(HistoryPaletteListItem.FromEntry(entry, text));
                }
            }
            catch (Exception exception)
            {
                FileLogger.Error($"Failed to reveal capture history entry '{entry.Id}'.", exception);
            }
        }

        return items;
    }
}
