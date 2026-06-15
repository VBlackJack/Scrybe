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
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrybe.App.Services;
using Scrybe.Core;
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
    private SnippetParameterEditorViewModel? _selectedParameter;

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

    /// <summary>The editable parameter definitions for the current snippet.</summary>
    public ObservableCollection<SnippetParameterEditorViewModel> ParameterRows { get; } = [];

    /// <summary>Reloads the visible snippets from the current library state.</summary>
    public void Reload()
    {
        string? selectedId = SelectedSnippet?.Id;
        Snippets.Clear();
        foreach (Snippet snippet in _library.Snippets)
        {
            Snippets.Add(snippet);
        }

        SelectedSnippet = Snippets.FirstOrDefault(snippet => string.Equals(snippet.Id, selectedId, StringComparison.Ordinal))
            ?? Snippets.FirstOrDefault();
    }

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
        LoadParameterRows(value.Parameters);
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
        ParameterRows.Clear();
        SelectedParameter = null;
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
        IReadOnlyList<SnippetParameter> parameters = BuildParameters();
        if (!ValidateTemplateParameters(parameters, out string missingParameters))
        {
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                _localization["Manager.ParametersMissing"],
                missingParameters);
            IsStatusError = true;
            return;
        }

        ParametersText = FormatParameters(parameters);

        Snippet snippet = new(
            id,
            Name.Trim(),
            string.IsNullOrWhiteSpace(Category) ? null : Category.Trim(),
            Template,
            parameters);

        bool persisted = await _library.SaveAsync(snippet).ConfigureAwait(true);
        Reload();
        SelectedSnippet = Snippets.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
        if (!persisted)
        {
            ShowSaveFailure();
            return;
        }

        StatusMessage = _localization["Manager.Saved"];
        IsStatusError = false;
    }

    [RelayCommand]
    private void AddParameter()
    {
        SnippetParameterEditorViewModel row = new();
        ParameterRows.Add(row);
        SelectedParameter = row;
    }

    [RelayCommand]
    private void RemoveParameter()
    {
        if (SelectedParameter is null)
        {
            return;
        }

        int index = ParameterRows.IndexOf(SelectedParameter);
        ParameterRows.Remove(SelectedParameter);
        SelectedParameter = ParameterRows.Count == 0
            ? null
            : ParameterRows[Math.Clamp(index, 0, ParameterRows.Count - 1)];
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
        if (!_confirmation.ConfirmDanger(_localization["Manager.DeleteConfirmTitle"], message, _localization["Dialog.Delete"]))
        {
            StatusMessage = string.Empty;
            IsStatusError = false;
            return;
        }

        bool persisted = await _library.DeleteAsync(SelectedSnippet.Id).ConfigureAwait(true);
        Reload();
        New();
        if (!persisted)
        {
            ShowSaveFailure();
            return;
        }

        StatusMessage = _localization["Manager.Deleted"];
        IsStatusError = false;
    }

    private void ShowSaveFailure()
    {
        StatusMessage = _localization["Persist.SaveFailed"];
        IsStatusError = true;
    }

    private void LoadParameterRows(IReadOnlyList<SnippetParameter> parameters)
    {
        ParameterRows.Clear();
        foreach (SnippetParameter parameter in parameters)
        {
            ParameterRows.Add(new SnippetParameterEditorViewModel(parameter));
        }

        SelectedParameter = ParameterRows.FirstOrDefault();
    }

    private IReadOnlyList<SnippetParameter> BuildParameters()
    {
        List<SnippetParameter> parameters = [];
        if (ParameterRows.Any(row => row.HasContent))
        {
            foreach (SnippetParameterEditorViewModel row in ParameterRows)
            {
                string name = row.Name.Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                parameters.Add(row.ToParameter());
            }

            return parameters;
        }

        return ParseParameters(ParametersText);
    }

    private bool ValidateTemplateParameters(IReadOnlyList<SnippetParameter> parameters, out string missingParameters)
    {
        HashSet<string> defined = parameters.Select(parameter => parameter.Name).ToHashSet(StringComparer.Ordinal);
        List<string> missing = [];
        foreach (Match match in Regex.Matches(Template, AppConstants.SnippetPlaceholderPattern))
        {
            string name = match.Groups[1].Value;
            if (name.Length == 0 || defined.Contains(name) || missing.Contains(name))
            {
                continue;
            }

            missing.Add(name);
        }

        missingParameters = string.Join(", ", missing);
        return missing.Count == 0;
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
