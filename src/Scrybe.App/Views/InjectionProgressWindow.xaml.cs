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

namespace Scrybe.App.Views;

/// <summary>A non-activating progress surface; closing it requests cancellation.</summary>
public sealed partial class InjectionProgressWindow : Window
{
    private readonly InjectionProgressViewModel _viewModel;
    /// <summary>Creates a progress surface without taking the confirmed target's focus.</summary>
    public InjectionProgressWindow(InjectionProgressViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Left = SystemParameters.WorkArea.Left;
        Top = SystemParameters.WorkArea.Top;
    }
    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.IsRunning) { _viewModel.StopCommand.Execute(null); }
        base.OnClosing(e);
    }
}
