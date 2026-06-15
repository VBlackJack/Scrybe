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

/// <summary>Editable row for one snippet parameter definition.</summary>
public sealed partial class SnippetParameterEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _defaultValue = string.Empty;

    /// <summary>Initializes an empty parameter row.</summary>
    public SnippetParameterEditorViewModel()
    {
    }

    /// <summary>Initializes a parameter row from a persisted definition.</summary>
    /// <param name="parameter">Persisted snippet parameter.</param>
    public SnippetParameterEditorViewModel(SnippetParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        _name = parameter.Name;
        _label = parameter.Label;
        _defaultValue = parameter.Default ?? string.Empty;
    }

    /// <summary>Whether this row contains any user-entered value.</summary>
    public bool HasContent =>
        !string.IsNullOrWhiteSpace(Name)
        || !string.IsNullOrWhiteSpace(Label)
        || !string.IsNullOrWhiteSpace(DefaultValue);

    /// <summary>Converts a non-empty editor row to the persisted model.</summary>
    public SnippetParameter ToParameter()
    {
        string name = Name.Trim();
        string label = string.IsNullOrWhiteSpace(Label) ? name : Label.Trim();
        string? defaultValue = string.IsNullOrWhiteSpace(DefaultValue) ? null : DefaultValue.Trim();
        return new SnippetParameter(name, label, defaultValue);
    }
}
