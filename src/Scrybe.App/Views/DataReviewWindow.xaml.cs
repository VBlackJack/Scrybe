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

namespace Scrybe.App.Views;

/// <summary>Modal operation preview; actual writes remain in the operation service after confirmation.</summary>
public sealed partial class DataReviewWindow : Window
{
    /// <summary>Creates the modal presenter.</summary>
    public DataReviewWindow(DataReviewViewModel viewModel)
    {
        InitializeComponent(); DataContext = viewModel;
        viewModel.Completed += OnCompleted;
        Closed += (_, _) => viewModel.Completed -= OnCompleted;
    }
    private void OnCompleted(object? sender, bool accepted) => DialogResult = accepted;
}
