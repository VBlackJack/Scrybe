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

using System.Diagnostics;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;

namespace Scrybe.App.Services;

public sealed partial class InjectionCoordinator
{
    private const int ProgressIntervalMs = 50;
    private long _lastProgressTimestamp;
    private InjectionProgress _progress = new(string.Empty, 0, 0, false, "Inject.Ready");

    /// <summary>Raised on the injection thread; UI subscribers must marshal to their dispatcher.</summary>
    public event EventHandler<InjectionProgress>? ProgressChanged;

    private IInjectionContext? Track(IInjectionContext? context, int total)
    {
        _progress = new(context?.TargetDisplay ?? string.Empty, 0, total, true, "Inject.Running");
        ProgressChanged?.Invoke(this, _progress);
        return context is null ? null : new ReportingContext(context, (completed, count) =>
        {
            _progress = _progress with { Completed = completed, Total = count };
            if (completed == count || Stopwatch.GetElapsedTime(_lastProgressTimestamp).TotalMilliseconds >= ProgressIntervalMs)
            {
                _lastProgressTimestamp = Stopwatch.GetTimestamp();
                ProgressChanged?.Invoke(this, _progress);
            }
        });
    }

    private void FinishProgress(InjectionResult result)
    {
        string key = result.Reason switch
        {
            InjectionFailureReason.TargetChanged => "Inject.TargetChanged",
            InjectionFailureReason.Unmappable => "Inject.Unmappable",
            InjectionFailureReason.NativeFailure => "Inject.NativeFailure",
            _ when result.UipiBlocked => "Inject.UipiBlocked",
            _ when result.Aborted => "Inject.Aborted",
            _ when result.Success => "Inject.Completed",
            _ => "Inject.Failed"
        };
        _progress = _progress with { IsRunning = false, StatusKey = key };
        ProgressChanged?.Invoke(this, _progress);
    }

    private sealed class ReportingContext(IInjectionContext inner, Action<int, int> report) : IInjectionContext
    {
        public bool IsCurrent => inner.IsCurrent;
        public IntPtr KeyboardLayout => inner.KeyboardLayout;
        public string TargetDisplay => inner.TargetDisplay;
        public InjectionProfile? Profile => inner.Profile;
        public void ReportProgress(int completed, int total)
        {
            inner.ReportProgress(completed, total);
            report(completed, total);
        }
    }
}
