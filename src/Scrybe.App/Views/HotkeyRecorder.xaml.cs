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

using System.Windows;
using Scrybe.App.Services;
using Scrybe.App.ViewModels;
using Key = System.Windows.Input.Key;
using Keyboard = System.Windows.Input.Keyboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace Scrybe.App.Views;

/// <summary>
/// A recorder for a single global hotkey. Clicking "Record" captures the next pressed combo via
/// <see cref="UIElement.PreviewKeyDown"/>; Escape cancels. Key translation is delegated to Core-facing
/// helpers, so this code-behind is limited to keyboard interop.
/// </summary>
public sealed partial class HotkeyRecorder : UserControl
{
    /// <summary>Initializes the recorder control.</summary>
    public HotkeyRecorder()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private HotkeyRecorderViewModel? ViewModel => DataContext as HotkeyRecorderViewModel;

    private void OnRecordClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.StartRecording();
        Focus();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        HotkeyRecorderViewModel? viewModel = ViewModel;
        if (viewModel is null || !viewModel.IsRecording)
        {
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        e.Handled = true;

        if (key == Key.Escape)
        {
            viewModel.Cancel();
            return;
        }

        if (HotkeyKeyTranslator.IsModifierKey(key))
        {
            return;
        }

        string? token = HotkeyKeyTranslator.TranslateKey(key);
        if (token is not null)
        {
            viewModel.Capture(HotkeyKeyTranslator.TranslateModifiers(Keyboard.Modifiers), token);
        }
    }
}
