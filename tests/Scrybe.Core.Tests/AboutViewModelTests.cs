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

using FluentAssertions;
using Scrybe.App.Services;
using Scrybe.App.ViewModels;
using Scrybe.Core;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for the About tab view model.</summary>
public sealed class AboutViewModelTests
{
    [Fact]
    public void Refresh_ExposesLocalizedDiagnosticRows()
    {
        TestLocalizationManager localization = new();
        AppSettings settings = new()
        {
            CapturesDirectory = Path.Combine(Path.GetTempPath(), "ScrybeDiagnostics", Guid.NewGuid().ToString("N")),
        };

        AboutViewModel viewModel = new(
            localization,
            new AboutInfoProvider(),
            new DiagnosticsInfoProvider(settings),
            new RecordingClipboardService(),
            new RecordingSystemShell());

        viewModel.DiagnosticsTitle.Should().Be("Runtime diagnostics");
        viewModel.DiagnosticRows.Should().Contain(row =>
            row.Label == "Settings file"
            && row.Value.EndsWith(Path.Combine(AppConstants.AppName, AppConstants.SettingsFileName), StringComparison.Ordinal));
        viewModel.DiagnosticRows.Should().Contain(row =>
            row.Label == "OCR data"
            && row.HasStatus
            && (row.Status == "Present" || row.Status == "Missing"));

        localization.Set("Diagnostics.Title", "Diagnostics runtime");
        localization.RaiseLocaleChanged();

        viewModel.DiagnosticsTitle.Should().Be("Diagnostics runtime");
    }

    [Fact]
    public async Task CopyDiagnosticReportCommand_CopiesLocalizedReportAndSetsStatus()
    {
        TestLocalizationManager localization = new();
        RecordingClipboardService clipboard = new();
        AboutViewModel viewModel = new(
            localization,
            new AboutInfoProvider(),
            new DiagnosticsInfoProvider(new AppSettings()),
            clipboard,
            new RecordingSystemShell());

        await viewModel.CopyDiagnosticReportCommand.ExecuteAsync(null);

        clipboard.Text.Should().Contain("Scrybe - Runtime diagnostics");
        clipboard.Text.Should().Contain("Settings file:");
        viewModel.StatusMessage.Should().Be("Report copied");
        viewModel.IsStatusError.Should().BeFalse();
    }

    [Fact]
    public void OpenLogsDirectoryCommand_UsesShellAndReportsFailure()
    {
        RecordingSystemShell shell = new()
        {
            ShouldOpen = false,
        };
        AboutViewModel viewModel = new(
            new TestLocalizationManager(),
            new AboutInfoProvider(),
            new DiagnosticsInfoProvider(new AppSettings()),
            new RecordingClipboardService(),
            shell);

        viewModel.OpenLogsDirectoryCommand.Execute(null);

        shell.Directory.Should().EndWith(Path.Combine(AppConstants.AppName, AppConstants.LogSubDirName));
        viewModel.IsStatusError.Should().BeTrue();
        viewModel.StatusMessage.Should().Contain("Could not open");
    }

    private sealed class TestLocalizationManager : ILocalizationManager
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal)
        {
            ["About.Title"] = "About Scrybe",
            ["About.Version"] = "Version",
            ["About.BuildDate"] = "Build date",
            ["About.Commit"] = "Source revision",
            ["About.License"] = "License",
            ["About.Copyright"] = "Copyright",
            ["AppTitle"] = "Scrybe",
            ["AppTagline"] = "OCR",
            ["Diagnostics.Title"] = "Runtime diagnostics",
            ["Diagnostics.Present"] = "Present",
            ["Diagnostics.Missing"] = "Missing",
            ["Diagnostics.AppDataDirectory"] = "App data",
            ["Diagnostics.SettingsFile"] = "Settings file",
            ["Diagnostics.LogsDirectory"] = "Logs",
            ["Diagnostics.CapturesDirectory"] = "Captures",
            ["Diagnostics.SnippetsFile"] = "Snippets",
            ["Diagnostics.SecretsFile"] = "Secrets",
            ["Diagnostics.HistoryFile"] = "History",
            ["Diagnostics.AppBaseDirectory"] = "App base",
            ["Diagnostics.LocalesDirectory"] = "Locales",
            ["Diagnostics.TessdataDirectory"] = "Tessdata",
            ["Diagnostics.OcrLanguage"] = "OCR language",
            ["Diagnostics.OcrTrainedData"] = "OCR data",
            ["Diagnostics.TesseractManagedAssembly"] = "Tesseract .NET",
            ["Diagnostics.TesseractNativeAssembly"] = "Tesseract native",
            ["Diagnostics.LeptonicaNativeAssembly"] = "Leptonica native",
            ["Diagnostics.ReportCopied"] = "Report copied",
            ["Diagnostics.LogsOpened"] = "Logs opened",
            ["Diagnostics.AppDataOpened"] = "App data opened",
            ["Diagnostics.OpenFailed"] = "Could not open {0}",
        };

        public string this[string key] => _values.GetValueOrDefault(key, key);

        public string Current => "en";

        public event EventHandler? LocaleChanged;

        public Task LoadAsync(string localeCode, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Set(string key, string value) => _values[key] = value;

        public void RaiseLocaleChanged() => LocaleChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RecordingClipboardService : IClipboardService
    {
        public string Text { get; private set; } = string.Empty;

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(Text);

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            Text = text;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSystemShell : ISystemShell
    {
        public bool ShouldOpen { get; init; } = true;

        public string Directory { get; private set; } = string.Empty;

        public bool TryOpenDirectory(string directory)
        {
            Directory = directory;
            return ShouldOpen;
        }
    }
}
