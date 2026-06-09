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

using Scrybe.App.ViewModels;
using Scrybe.App.Views;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;

namespace Scrybe.App.Services;

/// <summary>
/// Opens the snippet palette and routes a confirmed snippet to the injection engine. Before injecting
/// it restores foreground to the console that owned focus when the palette opened, so the resolved text
/// is typed into the console and not into the palette.
/// </summary>
public sealed class SnippetCoordinator
{
    private readonly SnippetLibrary _library;
    private readonly InjectionCoordinator _injection;
    private readonly ITargetWindowGateway _targetGateway;
    private readonly INotificationService _notification;
    private readonly ILocalizationManager _localization;

    private SnippetPaletteWindow? _palette;

    /// <summary>Initializes the coordinator with snippet injection dependencies.</summary>
    /// <param name="library">The snippet library.</param>
    /// <param name="injection">The injection coordinator used to type the resolved text.</param>
    /// <param name="targetGateway">Gateway used to capture and restore the target window.</param>
    /// <param name="notification">Notification service for restore failures.</param>
    /// <param name="localization">Localization source for user-facing messages.</param>
    public SnippetCoordinator(
        SnippetLibrary library,
        InjectionCoordinator injection,
        ITargetWindowGateway targetGateway,
        INotificationService notification,
        ILocalizationManager localization)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(injection);
        ArgumentNullException.ThrowIfNull(targetGateway);
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(localization);
        _library = library;
        _injection = injection;
        _targetGateway = targetGateway;
        _notification = notification;
        _localization = localization;
    }

    /// <summary>Opens (or focuses) the snippet palette, remembering the current foreground console.</summary>
    public void ShowPalette()
    {
        if (_palette is not null)
        {
            _palette.Activate();
            return;
        }

        IntPtr target = _targetGateway.GetForegroundWindow();
        SnippetPaletteViewModel viewModel = new(_library.Snippets);
        SnippetPaletteWindow window = new(viewModel);

        viewModel.InjectRequested += (_, text) =>
        {
            window.Close();
            _ = InjectResolvedSnippetAsync(target, text);
        };
        window.Closed += (_, _) => _palette = null;

        _palette = window;
        window.Show();
        window.Activate();
    }

    /// <summary>Restores the captured target and injects a resolved snippet only if restore succeeds.</summary>
    /// <param name="target">Captured target window handle.</param>
    /// <param name="text">Resolved snippet text to type.</param>
    /// <returns><see langword="true"/> when injection was started after a successful restore.</returns>
    public async Task<bool> InjectResolvedSnippetAsync(IntPtr target, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!_targetGateway.TryRestore(target))
        {
            FileLogger.Warn("Snippet target restore failed; snippet injection aborted.");
            _notification.Notify(_localization["AppTitle"], _localization["Palette.TargetUnavailable"]);
            return false;
        }

        FileLogger.Info("Snippet injection requested from palette.");
        await _injection.InjectTextAsync(text).ConfigureAwait(false);
        return true;
    }
}
