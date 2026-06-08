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
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrybe.App.Services;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;

namespace Scrybe.App.ViewModels;

/// <summary>
/// View model for the snippet management view. Parameters are edited as one
/// <c>name|label|default</c> line each, keeping the editor simple while persisting via the library.
/// </summary>
public sealed partial class SnippetManagerViewModel : ObservableObject
{
    private const char FieldSeparator = '|';
    private const char LineSeparator = '\n';

    private readonly SnippetLibrary _library;
    private readonly ILocalizationManager _localization;
    private readonly IConfirmationService _confirmation;

    [ObservableProperty]
    private Snippet? _selectedSnippet;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _template = string.Empty;

    [ObservableProperty]
    private string _parametersText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    /// <summary>Initializes the manager from the snippet library.</summary>
    /// <param name="library">The snippet library.</param>
    /// <param name="localization">Localization source for status text.</param>
    /// <param name="confirmation">Confirmation service for destructive actions.</param>
    public SnippetManagerViewModel(
        SnippetLibrary library,
        ILocalizationManager localization,
        IConfirmationService confirmation)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(confirmation);
        _library = library;
        _localization = localization;
        _confirmation = confirmation;
        Snippets = new ObservableCollection<Snippet>(library.Snippets);
    }

    /// <summary>The snippets shown in the list.</summary>
    public ObservableCollection<Snippet> Snippets { get; }

    partial void OnSelectedSnippetChanged(Snippet? value)
    {
        if (value is null)
        {
            return;
        }

        Name = value.Name;
        Category = value.Category ?? string.Empty;
        Template = value.Template;
        ParametersText = FormatParameters(value.Parameters);
        StatusMessage = string.Empty;
        IsStatusError = false;
    }

    [RelayCommand]
    private void New()
    {
        SelectedSnippet = null;
        Name = string.Empty;
        Category = string.Empty;
        Template = string.Empty;
        ParametersText = string.Empty;
        StatusMessage = string.Empty;
        IsStatusError = false;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Template))
        {
            StatusMessage = _localization["Manager.NameTemplateRequired"];
            IsStatusError = true;
            return;
        }

        string id = SelectedSnippet?.Id ?? Guid.NewGuid().ToString("N");
        Snippet snippet = new(
            id,
            Name.Trim(),
            string.IsNullOrWhiteSpace(Category) ? null : Category.Trim(),
            Template,
            ParseParameters(ParametersText));

        await _library.SaveAsync(snippet).ConfigureAwait(true);
        Refresh();
        SelectedSnippet = Snippets.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
        StatusMessage = _localization["Manager.Saved"];
        IsStatusError = false;
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedSnippet is null)
        {
            return;
        }

        string snippetName = SelectedSnippet.Name;
        string message = string.Format(
            CultureInfo.CurrentCulture,
            _localization["Manager.DeleteConfirmMessage"],
            snippetName);
        if (!_confirmation.ConfirmDanger(_localization["Manager.DeleteConfirmTitle"], message))
        {
            StatusMessage = string.Empty;
            IsStatusError = false;
            return;
        }

        await _library.DeleteAsync(SelectedSnippet.Id).ConfigureAwait(true);
        Refresh();
        New();
        StatusMessage = _localization["Manager.Deleted"];
        IsStatusError = false;
    }

    private void Refresh()
    {
        Snippets.Clear();
        foreach (Snippet snippet in _library.Snippets)
        {
            Snippets.Add(snippet);
        }
    }

    private static string FormatParameters(IReadOnlyList<SnippetParameter> parameters)
        => string.Join(LineSeparator, parameters.Select(p => $"{p.Name}{FieldSeparator}{p.Label}{FieldSeparator}{p.Default}"));

    private static IReadOnlyList<SnippetParameter> ParseParameters(string text)
    {
        List<SnippetParameter> parameters = [];
        foreach (string rawLine in text.Split(LineSeparator))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            string[] parts = line.Split(FieldSeparator);
            string name = parts[0].Trim();
            if (name.Length == 0)
            {
                continue;
            }

            string label = parts.Length > 1 && parts[1].Trim().Length > 0 ? parts[1].Trim() : name;
            string? defaultValue = parts.Length > 2 && parts[2].Trim().Length > 0 ? parts[2].Trim() : null;
            parameters.Add(new SnippetParameter(name, label, defaultValue));
        }

        return parameters;
    }
}
