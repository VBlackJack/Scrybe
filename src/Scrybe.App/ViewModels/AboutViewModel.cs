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
using Scrybe.App.Services;
using Scrybe.Core.Interfaces;

namespace Scrybe.App.ViewModels;

/// <summary>View model for the About tab.</summary>
public sealed class AboutViewModel : ObservableObject
{
    private const string UnknownValue = "-";

    private readonly ILocalizationManager _localization;
    private readonly AboutInfoProvider _aboutInfoProvider;
    private string _title = string.Empty;
    private string _appName = string.Empty;
    private string _tagline = string.Empty;
    private string _version = string.Empty;
    private IReadOnlyList<AboutDetailRow> _rows = [];

    /// <summary>Initializes the About view model from localization and assembly metadata.</summary>
    public AboutViewModel(ILocalizationManager localization, AboutInfoProvider aboutInfoProvider)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(aboutInfoProvider);

        _localization = localization;
        _aboutInfoProvider = aboutInfoProvider;

        Refresh();
        localization.LocaleChanged += OnLocaleChanged;
    }

    /// <summary>Localized window title.</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>Localized application name.</summary>
    public string AppName
    {
        get => _appName;
        private set => SetProperty(ref _appName, value);
    }

    /// <summary>Localized tagline.</summary>
    public string Tagline
    {
        get => _tagline;
        private set => SetProperty(ref _tagline, value);
    }

    /// <summary>Display version shown in the header.</summary>
    public string Version
    {
        get => _version;
        private set => SetProperty(ref _version, value);
    }

    /// <summary>Localized detail rows.</summary>
    public IReadOnlyList<AboutDetailRow> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    private void OnLocaleChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        AboutInfo aboutInfo = _aboutInfoProvider.GetAboutInfo();

        Title = _localization["About.Title"];
        AppName = _localization["AppTitle"];
        Tagline = _localization["AppTagline"];
        Version = aboutInfo.Version;

        List<AboutDetailRow> rows = [];
        AddRow(rows, _localization["About.Version"], aboutInfo.Version, includeUnknown: true);
        AddRow(rows, _localization["About.BuildDate"], aboutInfo.BuildDate);
        AddRow(rows, _localization["About.Commit"], aboutInfo.CommitHash, includeUnknown: true);
        AddRow(rows, _localization["About.License"], aboutInfo.License, includeUnknown: true);
        AddRow(rows, _localization["About.Copyright"], aboutInfo.Copyright, includeUnknown: true);
        Rows = rows;
    }

    private static void AddRow(List<AboutDetailRow> rows, string label, string value, bool includeUnknown = false)
    {
        if (includeUnknown || IsKnown(value))
        {
            rows.Add(new AboutDetailRow(label, value));
        }
    }

    private static bool IsKnown(string value)
        => !string.IsNullOrWhiteSpace(value)
        && !string.Equals(value.Trim(), UnknownValue, StringComparison.Ordinal);
}
