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
using Scrybe.App.ViewModels;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Scrybe.App.Views;

/// <summary>The capture-history palette window. Code-behind is limited to data-context wiring and key handling.</summary>
public sealed partial class HistoryPaletteWindow : Window
{
    /// <summary>Initializes the palette bound to its view model.</summary>
    /// <param name="viewModel">The palette view model.</param>
    public HistoryPaletteWindow(HistoryPaletteViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
        else if (e.Key == Key.Enter
            && DataContext is HistoryPaletteViewModel viewModel
            && viewModel.CanCopy)
        {
            viewModel.CopyCommand.Execute(null);
            e.Handled = true;
        }
    }
}
