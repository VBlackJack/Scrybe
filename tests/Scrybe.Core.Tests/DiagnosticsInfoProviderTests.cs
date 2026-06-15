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
using Scrybe.Core;
using Scrybe.Core.Models;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for runtime diagnostics surfaced by the app shell.</summary>
public sealed class DiagnosticsInfoProviderTests
{
    [Fact]
    public void GetDiagnosticsInfo_ReportsConfiguredPathsAndRuntimeAssetState()
    {
        string capturesDirectory = Path.Combine(Path.GetTempPath(), "ScrybeDiagnostics", Guid.NewGuid().ToString("N"));
        AppSettings settings = new()
        {
            CapturesDirectory = capturesDirectory,
            OcrLanguage = "fra",
        };
        DiagnosticsInfoProvider provider = new(settings);

        IReadOnlyList<DiagnosticInfoRow> rows = provider.GetDiagnosticsInfo();

        rows.Should().Contain(row => row.LabelKey == "Diagnostics.CapturesDirectory" && row.Value == capturesDirectory);
        rows.Should().Contain(row => row.LabelKey == "Diagnostics.OcrLanguage" && row.Value == "fra");
        rows.Should().Contain(row =>
            row.LabelKey == "Diagnostics.SettingsFile"
            && row.Value.EndsWith(Path.Combine(AppConstants.AppName, AppConstants.SettingsFileName), StringComparison.Ordinal));

        DiagnosticInfoRow trainedData = rows.Single(row => row.LabelKey == "Diagnostics.OcrTrainedData");
        trainedData.Value.Should().EndWith(Path.Combine(AppConstants.TessdataDirName, "fra.traineddata"));
        trainedData.Exists.Should().Be(File.Exists(trainedData.Value));
    }
}
