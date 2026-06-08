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

using System.ComponentModel;
using System.Windows;
using Scrybe.App.ViewModels;

namespace Scrybe.App;

/// <summary>
/// The application's main window. Code-behind is limited to lifecycle wiring: the
/// injected view model is assigned as the data context.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Initializes the window and binds it to its view model.</summary>
    /// <param name="viewModel">The main view model supplied by dependency injection.</param>
    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        if (System.Windows.Application.Current is App app && app.IsShutdownRequested)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
