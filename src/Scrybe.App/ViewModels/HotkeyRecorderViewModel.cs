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

namespace Scrybe.App.ViewModels;

/// <summary>State for a single editable hotkey: its captured modifiers/key, label, and recording flag.</summary>
public sealed partial class HotkeyRecorderViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    private string _modifiers;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    private string _key;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    private bool _isRecording;

    /// <summary>Initializes the recorder for an action from its current binding.</summary>
    /// <param name="actionId">The action identifier this hotkey triggers.</param>
    /// <param name="label">The localized action label.</param>
    /// <param name="modifiers">The current modifier tokens.</param>
    /// <param name="key">The current key token.</param>
    public HotkeyRecorderViewModel(string actionId, string label, string modifiers, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ActionId = actionId;
        Label = label;
        _modifiers = modifiers;
        _key = key;
    }

    /// <summary>The action this hotkey triggers.</summary>
    public string ActionId { get; }

    /// <summary>The localized action label.</summary>
    public string Label { get; }

    /// <summary>The combo shown to the user (an ellipsis while recording).</summary>
    public string Display => IsRecording
        ? "…"
        : string.IsNullOrEmpty(Modifiers) ? Key : $"{Modifiers}+{Key}";

    /// <summary>Begins capturing the next pressed combo.</summary>
    public void StartRecording() => IsRecording = true;

    /// <summary>Stores a captured combo and ends recording.</summary>
    /// <param name="modifiers">The captured modifier tokens.</param>
    /// <param name="key">The captured key token.</param>
    public void Capture(string modifiers, string key)
    {
        Modifiers = modifiers;
        Key = key;
        IsRecording = false;
    }

    /// <summary>Ends recording without changing the combo.</summary>
    public void Cancel() => IsRecording = false;
}
