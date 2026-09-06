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

/// <summary>Localized label with an opaque operation value.</summary>
public sealed record ReviewChoice(string Label, object Value);

/// <summary>Reusable modal selection and review. Closing without acceptance performs no write.</summary>
public sealed partial class DataReviewViewModel : ObservableObject
{
    /// <summary>Creates a review containing non-secret descriptions only.</summary>
    public DataReviewViewModel(string title, string summary, IReadOnlyList<ReviewChoice> entries, IReadOnlyList<ReviewChoice> policies)
    {
        Title = title; Summary = summary; Entries = entries; Policies = policies;
        _selectedEntry = entries.FirstOrDefault();
        _selectedPolicy = policies.FirstOrDefault();
    }
    /// <summary>Localized operation title.</summary>
    public string Title { get; }
    /// <summary>Preview and scope explanation.</summary>
    public string Summary { get; }
    /// <summary>Selectable versions or incoming snippet descriptions.</summary>
    public IReadOnlyList<ReviewChoice> Entries { get; }
    /// <summary>Available conflict policies; empty for backup browsing.</summary>
    public IReadOnlyList<ReviewChoice> Policies { get; }
    /// <summary>Whether the policy selector is relevant.</summary>
    public bool HasPolicies => Policies.Count > 0;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedDetails))]
    private ReviewChoice? _selectedEntry;
    [ObservableProperty] private ReviewChoice? _selectedPolicy;
    /// <summary>Displays the selected incoming template for inspection before import.</summary>
    public string SelectedDetails => SelectedEntry?.Value is Scrybe.Core.Models.Snippet snippet
        ? snippet.Template + Environment.NewLine + Environment.NewLine
            + string.Join(Environment.NewLine, snippet.Parameters.Select(parameter => $"{parameter.Name} | {parameter.Label} | {parameter.Default}"))
        : string.Empty;
    /// <summary>Signals modal completion.</summary>
    public event EventHandler<bool>? Completed;
    [RelayCommand] private void Accept() => Completed?.Invoke(this, true);
    [RelayCommand] private void Cancel() => Completed?.Invoke(this, false);
}
