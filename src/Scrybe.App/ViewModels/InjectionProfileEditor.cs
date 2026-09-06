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
using Scrybe.Core;
using Scrybe.Core.Models;

namespace Scrybe.App.ViewModels;

/// <summary>Editable profile draft, detached from settings used by active injections.</summary>
public sealed partial class InjectionProfileEditor : ObservableObject
{
    [ObservableProperty] private string _processName = string.Empty;
    [ObservableProperty] private InjectionMode _mode;
    [ObservableProperty] private string _keyDelayMs = AppConstants.InjectionKeyDelayMs.ToString(CultureInfo.CurrentCulture);
    [ObservableProperty] private string _enterExtraDelayMs = AppConstants.InjectionEnterExtraDelayMs.ToString(CultureInfo.CurrentCulture);
    /// <summary>Creates the immutable candidate validated on save.</summary>
    public InjectionProfile Snapshot() => new(ProcessName.Trim(), Mode,
        int.TryParse(KeyDelayMs, out int delay) ? delay : -1,
        int.TryParse(EnterExtraDelayMs, out int extra) ? extra : -1);
}
