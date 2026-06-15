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
using System.Collections.Specialized;
using System.ComponentModel;
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
    private bool _suppressPendingChanges;
    private bool _suppressDiscardPrompt;
    private bool _restoringRejectedSelection;
    private Snippet? _selectionBeforeChange;

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

    [ObservableProperty]
    private bool _hasPendingChanges;

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
        ParameterRows.CollectionChanged += OnParameterRowsCollectionChanged;
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

    partial void OnSelectedSnippetChanging(Snippet? oldValue, Snippet? newValue)
    {
        _selectionBeforeChange = oldValue;
    }

    partial void OnSelectedSnippetChanged(Snippet? value)
    {
        if (_restoringRejectedSelection)
        {
            return;
        }

        if (!_suppressDiscardPrompt
            && HasPendingChanges
            && !ReferenceEquals(value, _selectionBeforeChange)
            && !ConfirmDiscardChanges())
        {
            _restoringRejectedSelection = true;
            try
            {
                SelectedSnippet = _selectionBeforeChange;
            }
            finally
            {
                _restoringRejectedSelection = false;
            }

            return;
        }

        if (value is null)
        {
            return;
        }

        LoadEditor(value);
    }

    partial void OnNameChanged(string value) => MarkPendingChanges();

    partial void OnCategoryChanged(string value) => MarkPendingChanges();

    partial void OnTemplateChanged(string value) => MarkPendingChanges();

    partial void OnParametersTextChanged(string value) => MarkPendingChanges();

    [RelayCommand]
    private void New()
    {
        if (!_suppressDiscardPrompt && !ConfirmDiscardChanges())
        {
            return;
        }

        _suppressDiscardPrompt = true;
        _suppressPendingChanges = true;
        try
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
            HasPendingChanges = false;
        }
        finally
        {
            _suppressPendingChanges = false;
            _suppressDiscardPrompt = false;
        }
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
        _suppressDiscardPrompt = true;
        try
        {
            Reload();
            SelectedSnippet = Snippets.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
        }
        finally
        {
            _suppressDiscardPrompt = false;
        }

        if (!persisted)
        {
            ShowSaveFailure();
            return;
        }

        StatusMessage = _localization["Manager.Saved"];
        IsStatusError = false;
        HasPendingChanges = false;
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
        MarkPendingChanges();
    }

    [RelayCommand]
    private void DetectParameters()
    {
        List<string> placeholders = ExtractTemplateParameterNames();
        if (placeholders.Count == 0)
        {
            StatusMessage = _localization["Manager.ParametersNoneDetected"];
            IsStatusError = false;
            return;
        }

        HashSet<string> existing = ParameterRows
            .Select(row => row.Name.Trim())
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        int added = 0;
        foreach (string name in placeholders)
        {
            if (!existing.Add(name))
            {
                continue;
            }

            ParameterRows.Add(new SnippetParameterEditorViewModel(new SnippetParameter(name, name, null)));
            added++;
        }

        StatusMessage = added == 0
            ? _localization["Manager.ParametersAlreadyDetected"]
            : string.Format(CultureInfo.CurrentCulture, _localization["Manager.ParametersDetected"], added);
        IsStatusError = false;
        MarkPendingChanges();
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
        _suppressDiscardPrompt = true;
        try
        {
            Reload();
            New();
        }
        finally
        {
            _suppressDiscardPrompt = false;
        }

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
        HasPendingChanges = true;
    }

    private void LoadEditor(Snippet value)
    {
        _suppressPendingChanges = true;
        try
        {
            Name = value.Name;
            Category = value.Category ?? string.Empty;
            Template = value.Template;
            ParametersText = FormatParameters(value.Parameters);
            LoadParameterRows(value.Parameters);
            StatusMessage = string.Empty;
            IsStatusError = false;
            HasPendingChanges = false;
        }
        finally
        {
            _suppressPendingChanges = false;
        }
    }

    private void LoadParameterRows(IReadOnlyList<SnippetParameter> parameters)
    {
        foreach (SnippetParameterEditorViewModel row in ParameterRows)
        {
            row.PropertyChanged -= OnParameterRowPropertyChanged;
        }

        ParameterRows.Clear();
        foreach (SnippetParameter parameter in parameters)
        {
            SnippetParameterEditorViewModel row = new(parameter);
            ParameterRows.Add(row);
        }

        SelectedParameter = ParameterRows.FirstOrDefault();
    }

    private void OnParameterRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (SnippetParameterEditorViewModel row in e.OldItems)
            {
                row.PropertyChanged -= OnParameterRowPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (SnippetParameterEditorViewModel row in e.NewItems)
            {
                row.PropertyChanged += OnParameterRowPropertyChanged;
            }
        }

        MarkPendingChanges();
    }

    private void OnParameterRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SnippetParameterEditorViewModel.Name)
            or nameof(SnippetParameterEditorViewModel.Label)
            or nameof(SnippetParameterEditorViewModel.DefaultValue))
        {
            MarkPendingChanges();
        }
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
        foreach (string name in ExtractTemplateParameterNames())
        {
            if (name.Length == 0 || defined.Contains(name) || missing.Contains(name))
            {
                continue;
            }

            missing.Add(name);
        }

        missingParameters = string.Join(", ", missing);
        return missing.Count == 0;
    }

    private bool ConfirmDiscardChanges()
    {
        if (!HasPendingChanges)
        {
            return true;
        }

        return _confirmation.ConfirmDanger(
            _localization["Manager.DiscardConfirmTitle"],
            _localization["Manager.DiscardConfirmMessage"],
            _localization["Dialog.Discard"]);
    }

    private void MarkPendingChanges()
    {
        if (_suppressPendingChanges)
        {
            return;
        }

        HasPendingChanges = HasUnsavedChanges();
        if (HasPendingChanges && !IsStatusError)
        {
            StatusMessage = string.Empty;
        }
    }

    private bool HasUnsavedChanges()
    {
        if (SelectedSnippet is null)
        {
            return !string.IsNullOrWhiteSpace(Name)
                || !string.IsNullOrWhiteSpace(Category)
                || !string.IsNullOrWhiteSpace(Template)
                || BuildParameters().Count > 0;
        }

        return !Same(Name.Trim(), SelectedSnippet.Name)
            || !Same(NormalizeOptional(Category), SelectedSnippet.Category)
            || !Same(Template, SelectedSnippet.Template)
            || !ParametersEqual(BuildParameters(), SelectedSnippet.Parameters);
    }

    private static string? NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ParametersEqual(IReadOnlyList<SnippetParameter> left, IReadOnlyList<SnippetParameter> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            SnippetParameter leftParameter = left[index];
            SnippetParameter rightParameter = right[index];
            if (!Same(leftParameter.Name, rightParameter.Name)
                || !Same(leftParameter.Label, rightParameter.Label)
                || !Same(leftParameter.Default, rightParameter.Default))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left, right, StringComparison.Ordinal);

    private List<string> ExtractTemplateParameterNames()
    {
        List<string> names = [];
        foreach (Match match in Regex.Matches(Template, AppConstants.SnippetPlaceholderPattern))
        {
            string name = match.Groups[1].Value.Trim();
            if (name.Length > 0 && !names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
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
