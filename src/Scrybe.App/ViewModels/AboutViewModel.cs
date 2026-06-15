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

using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrybe.App.Services;
using Scrybe.Core.Interfaces;

namespace Scrybe.App.ViewModels;

/// <summary>View model for the About tab.</summary>
public sealed partial class AboutViewModel : ObservableObject
{
    private const string UnknownValue = "-";

    private readonly ILocalizationManager _localization;
    private readonly AboutInfoProvider _aboutInfoProvider;
    private readonly DiagnosticsInfoProvider _diagnosticsInfoProvider;
    private readonly IClipboardService _clipboard;
    private readonly ISystemShell _systemShell;
    private string _title = string.Empty;
    private string _appName = string.Empty;
    private string _tagline = string.Empty;
    private string _version = string.Empty;
    private string _diagnosticsTitle = string.Empty;
    private IReadOnlyList<AboutDetailRow> _rows = [];
    private IReadOnlyList<AboutDetailRow> _diagnosticRows = [];

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    /// <summary>Initializes the About view model from localization and assembly metadata.</summary>
    public AboutViewModel(
        ILocalizationManager localization,
        AboutInfoProvider aboutInfoProvider,
        DiagnosticsInfoProvider diagnosticsInfoProvider,
        IClipboardService clipboard,
        ISystemShell systemShell)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(aboutInfoProvider);
        ArgumentNullException.ThrowIfNull(diagnosticsInfoProvider);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(systemShell);

        _localization = localization;
        _aboutInfoProvider = aboutInfoProvider;
        _diagnosticsInfoProvider = diagnosticsInfoProvider;
        _clipboard = clipboard;
        _systemShell = systemShell;

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

    /// <summary>Localized diagnostics section title.</summary>
    public string DiagnosticsTitle
    {
        get => _diagnosticsTitle;
        private set => SetProperty(ref _diagnosticsTitle, value);
    }

    /// <summary>Localized detail rows.</summary>
    public IReadOnlyList<AboutDetailRow> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    /// <summary>Localized runtime diagnostic rows.</summary>
    public IReadOnlyList<AboutDetailRow> DiagnosticRows
    {
        get => _diagnosticRows;
        private set => SetProperty(ref _diagnosticRows, value);
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
        DiagnosticsTitle = _localization["Diagnostics.Title"];

        List<AboutDetailRow> rows = [];
        AddRow(rows, _localization["About.Version"], aboutInfo.Version, includeUnknown: true);
        AddRow(rows, _localization["About.BuildDate"], aboutInfo.BuildDate);
        AddRow(rows, _localization["About.Commit"], aboutInfo.CommitHash, includeUnknown: true);
        AddRow(rows, _localization["About.License"], aboutInfo.License, includeUnknown: true);
        AddRow(rows, _localization["About.Copyright"], aboutInfo.Copyright, includeUnknown: true);
        Rows = rows;

        DiagnosticRows = _diagnosticsInfoProvider
            .GetDiagnosticsInfo()
            .Select(row => new AboutDetailRow(_localization[row.LabelKey], FormatDiagnosticValue(row), row.Exists == false))
            .ToList();
    }

    [RelayCommand]
    private async Task CopyDiagnosticReport()
    {
        await _clipboard.SetTextAsync(BuildDiagnosticReport()).ConfigureAwait(true);
        StatusMessage = _localization["Diagnostics.ReportCopied"];
        IsStatusError = false;
    }

    [RelayCommand]
    private void OpenLogsDirectory()
        => OpenDirectory(_diagnosticsInfoProvider.GetLogsDirectory(), _localization["Diagnostics.LogsOpened"]);

    [RelayCommand]
    private void OpenAppDataDirectory()
        => OpenDirectory(_diagnosticsInfoProvider.GetAppDataDirectory(), _localization["Diagnostics.AppDataOpened"]);

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

    private string FormatDiagnosticValue(DiagnosticInfoRow row)
    {
        if (row.Exists is null)
        {
            return row.Value;
        }

        string state = row.Exists.Value
            ? _localization["Diagnostics.Present"]
            : _localization["Diagnostics.Missing"];
        return $"{state}: {row.Value}";
    }

    private void OpenDirectory(string directory, string successMessage)
    {
        if (_systemShell.TryOpenDirectory(directory))
        {
            StatusMessage = successMessage;
            IsStatusError = false;
            return;
        }

        StatusMessage = string.Format(
            CultureInfo.CurrentCulture,
            _localization["Diagnostics.OpenFailed"],
            directory);
        IsStatusError = true;
    }

    private string BuildDiagnosticReport()
    {
        StringBuilder builder = new();
        builder.AppendLine($"{_localization["AppTitle"]} - {_localization["Diagnostics.Title"]}");
        builder.AppendLine($"{_localization["About.Version"]}: {Version}");
        builder.AppendLine();

        builder.AppendLine(_localization["About.Title"]);
        foreach (AboutDetailRow row in Rows)
        {
            builder.AppendLine($"{row.Label}: {row.Value}");
        }

        builder.AppendLine();
        builder.AppendLine(_localization["Diagnostics.Title"]);
        foreach (AboutDetailRow row in DiagnosticRows)
        {
            builder.AppendLine($"{row.Label}: {row.Value}");
        }

        return builder.ToString();
    }
}
