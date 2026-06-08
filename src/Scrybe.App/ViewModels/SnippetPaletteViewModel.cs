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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrybe.Core.Models;
using Scrybe.Core.Snippets;

namespace Scrybe.App.ViewModels;

/// <summary>
/// View model for the snippet palette: pick a snippet, fill its parameters, see a live preview of the
/// resolved text, and request injection. Injection is enabled only when every parameter is filled.
/// </summary>
public sealed partial class SnippetPaletteViewModel : ObservableObject
{
    [ObservableProperty]
    private Snippet? _selectedSnippet;

    [ObservableProperty]
    private string _preview = string.Empty;

    [ObservableProperty]
    private bool _canInject;

    /// <summary>Initializes the palette with the available snippets.</summary>
    /// <param name="snippets">The snippet library.</param>
    public SnippetPaletteViewModel(IReadOnlyList<Snippet> snippets)
    {
        ArgumentNullException.ThrowIfNull(snippets);
        Snippets = new ObservableCollection<Snippet>(snippets);
    }

    /// <summary>Raised with the resolved text when the user confirms injection.</summary>
    public event EventHandler<string>? InjectRequested;

    /// <summary>The selectable snippets.</summary>
    public ObservableCollection<Snippet> Snippets { get; }

    /// <summary>The editable fields for the selected snippet's parameters.</summary>
    public ObservableCollection<ParameterFieldViewModel> Fields { get; } = [];

    partial void OnSelectedSnippetChanged(Snippet? value)
    {
        Fields.Clear();
        if (value is not null)
        {
            foreach (SnippetParameter parameter in value.Parameters)
            {
                Fields.Add(new ParameterFieldViewModel(parameter, UpdatePreview));
            }
        }

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (SelectedSnippet is null)
        {
            Preview = string.Empty;
            CanInject = false;
            return;
        }

        Dictionary<string, string?> values = new(StringComparer.Ordinal);
        foreach (ParameterFieldViewModel field in Fields)
        {
            values[field.Name] = field.Value;
        }

        TemplateRenderResult result = TemplateRenderer.Render(SelectedSnippet, values);
        Preview = result.Text;
        CanInject = result.IsComplete;
    }

    [RelayCommand]
    private void Inject()
    {
        if (CanInject)
        {
            InjectRequested?.Invoke(this, Preview);
        }
    }
}
