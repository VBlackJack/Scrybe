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

using Scrybe.App.Interop;
using Scrybe.App.ViewModels;
using Scrybe.App.Views;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;

namespace Scrybe.App.Services;

/// <summary>
/// Opens the snippet palette and management windows and routes a confirmed snippet to the injection
/// engine. Before injecting it restores foreground to the console that owned focus when the palette
/// opened, so the resolved text is typed into the console and not into the palette.
/// </summary>
public sealed class SnippetCoordinator
{
    private readonly SnippetLibrary _library;
    private readonly InjectionCoordinator _injection;
    private readonly ILocalizationManager _localization;
    private readonly IConfirmationService _confirmation;

    private SnippetPaletteWindow? _palette;
    private SnippetManagerWindow? _manager;

    /// <summary>Initializes the coordinator with the snippet library and the injection coordinator.</summary>
    /// <param name="library">The snippet library.</param>
    /// <param name="injection">The injection coordinator used to type the resolved text.</param>
    /// <param name="localization">Localization source for manager feedback.</param>
    /// <param name="confirmation">Confirmation service for destructive actions.</param>
    public SnippetCoordinator(
        SnippetLibrary library,
        InjectionCoordinator injection,
        ILocalizationManager localization,
        IConfirmationService confirmation)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(injection);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(confirmation);
        _library = library;
        _injection = injection;
        _localization = localization;
        _confirmation = confirmation;
    }

    /// <summary>Opens (or focuses) the snippet palette, remembering the current foreground console.</summary>
    public void ShowPalette()
    {
        if (_palette is not null)
        {
            _palette.Activate();
            return;
        }

        IntPtr target = InjectionInterop.GetForegroundWindowHandle();
        SnippetPaletteViewModel viewModel = new(_library.Snippets);
        SnippetPaletteWindow window = new(viewModel);

        viewModel.InjectRequested += (_, text) =>
        {
            window.Close();
            InjectionInterop.RestoreForeground(target);
            FileLogger.Info("Snippet injection requested from palette.");
            _ = _injection.InjectTextAsync(text);
        };
        window.Closed += (_, _) => _palette = null;

        _palette = window;
        window.Show();
        window.Activate();
    }

    /// <summary>Opens (or focuses) the snippet management window.</summary>
    public void ShowManager()
    {
        if (_manager is not null)
        {
            _manager.Activate();
            return;
        }

        SnippetManagerViewModel viewModel = new(_library, _localization, _confirmation);
        SnippetManagerWindow window = new(viewModel);
        window.Closed += (_, _) => _manager = null;

        _manager = window;
        window.Show();
        window.Activate();
    }
}
