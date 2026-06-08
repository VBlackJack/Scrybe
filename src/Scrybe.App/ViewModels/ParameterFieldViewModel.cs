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
using Scrybe.Core.Models;

namespace Scrybe.App.ViewModels;

/// <summary>An editable value for a single snippet parameter in the palette.</summary>
public sealed partial class ParameterFieldViewModel : ObservableObject
{
    private readonly Action _onValueChanged;

    /// <summary>The parameter value entered by the user (pre-filled with the default).</summary>
    [ObservableProperty]
    private string _value;

    /// <summary>Initializes the field from a parameter definition.</summary>
    /// <param name="parameter">The parameter definition.</param>
    /// <param name="onValueChanged">Callback invoked when the value changes (to refresh the preview).</param>
    public ParameterFieldViewModel(SnippetParameter parameter, Action onValueChanged)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(onValueChanged);

        Name = parameter.Name;
        Label = parameter.Label;
        _value = parameter.Default ?? string.Empty;
        _onValueChanged = onValueChanged;
    }

    /// <summary>The placeholder name this field fills.</summary>
    public string Name { get; }

    /// <summary>The human-readable label shown next to the field.</summary>
    public string Label { get; }

    partial void OnValueChanged(string value) => _onValueChanged();
}
