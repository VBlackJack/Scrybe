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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Scrybe.App.ViewModels;

/// <summary>Ephemeral correction draft. Cancel never publishes text to application stores.</summary>
public sealed partial class OcrReviewViewModel : ObservableObject
{
    [ObservableProperty]
    private string _text;

    /// <summary>Creates a draft preserving all recognized whitespace.</summary>
    public OcrReviewViewModel(string text) => _text = text;

    /// <summary>Requests acceptance or cancellation from the modal presenter.</summary>
    public event EventHandler<bool>? Completed;

    [RelayCommand]
    private void Accept() => Completed?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => Completed?.Invoke(this, false);
}
