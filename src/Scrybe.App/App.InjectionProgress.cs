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
using Scrybe.Core.Models;

namespace Scrybe.App;

public partial class App
{
    private bool _progressDismissed;
    private InjectionProgressWindow? _progressWindow;
    private InjectionProgressViewModel? _progressViewModel;
    private void OnInjectionProgressChanged(object? sender, InjectionProgress progress)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (progress.IsRunning && progress.Completed == 0) { _progressDismissed = false; }
            if (_progressWindow is null && progress.IsRunning && !_progressDismissed)
            {
                _progressViewModel = new(_localization!, () => _injectionCoordinator?.Abort());
                _progressWindow = new(_progressViewModel);
                _progressWindow.Closed += (_, _) => { _progressWindow = null; _progressDismissed = true; };
                _progressWindow.Show();
            }
            _progressViewModel?.Update(progress);
        });
    }
}
