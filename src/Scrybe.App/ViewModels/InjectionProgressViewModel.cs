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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;

namespace Scrybe.App.ViewModels;

/// <summary>Displays metadata only; never retains text or secrets being injected.</summary>
public sealed partial class InjectionProgressViewModel(ILocalizationManager localization, Action abort) : ObservableObject
{
    [ObservableProperty] private string _target = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _counts = string.Empty;
    [ObservableProperty] private int _completed;
    [ObservableProperty] private int _total = 1;
    [ObservableProperty][NotifyCanExecuteChangedFor(nameof(StopCommand))] private bool _isRunning;

    /// <summary>Called by the presenter on the UI thread.</summary>
    public void Update(InjectionProgress progress)
    {
        Target = progress.Target;
        Completed = progress.Completed;
        Total = Math.Max(1, progress.Total);
        IsRunning = progress.IsRunning;
        Status = localization[progress.StatusKey];
        Counts = string.Format(CultureInfo.CurrentCulture, localization["Inject.ProgressCounts"], progress.Completed, progress.Total);
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop() => abort();
}
