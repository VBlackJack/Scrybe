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

using System.IO;
using Scrybe.Core;
using Scrybe.Core.IO;
using Scrybe.Core.Models;

namespace Scrybe.App.Services;

/// <summary>Builds local runtime diagnostics shown in the About tab.</summary>
public sealed class DiagnosticsInfoProvider
{
    private const string TesseractManagedAssemblyFileName = "Tesseract.dll";
    private const string TesseractNativeAssemblyFileName = "tesseract50.dll";
    private const string LeptonicaNativeAssemblyFileName = "leptonica-1.82.0.dll";
    private const string NativeRuntimeDirectoryName = "x64";

    private readonly AppSettings _settings;

    /// <summary>Initializes diagnostics from the current application settings.</summary>
    public DiagnosticsInfoProvider(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
    }

    /// <summary>Gets local paths and runtime asset checks useful when troubleshooting a deployment.</summary>
    public IReadOnlyList<DiagnosticInfoRow> GetDiagnosticsInfo()
    {
        string appDataDirectory = GetAppDataDirectory();
        string appBaseDirectory = AppContext.BaseDirectory;
        string localesDirectory = Path.Combine(appBaseDirectory, AppConstants.LocalesDirName);
        string tessdataDirectory = Path.Combine(appBaseDirectory, AppConstants.TessdataDirName);
        string trainedDataPath = Path.Combine(tessdataDirectory, $"{_settings.OcrLanguage}.traineddata");
        string tesseractManagedPath = Path.Combine(appBaseDirectory, TesseractManagedAssemblyFileName);
        string tesseractNativePath = Path.Combine(appBaseDirectory, NativeRuntimeDirectoryName, TesseractNativeAssemblyFileName);
        string leptonicaNativePath = Path.Combine(appBaseDirectory, NativeRuntimeDirectoryName, LeptonicaNativeAssemblyFileName);

        return
        [
            new("Diagnostics.AppDataDirectory", appDataDirectory),
            new("Diagnostics.SettingsFile", Path.Combine(appDataDirectory, AppConstants.SettingsFileName)),
            new("Diagnostics.LogsDirectory", GetLogsDirectory()),
            new("Diagnostics.CapturesDirectory", CapturePathResolver.ResolveDirectory(_settings.CapturesDirectory)),
            new("Diagnostics.SnippetsFile", Path.Combine(appDataDirectory, AppConstants.SnippetsFileName)),
            new("Diagnostics.SecretsFile", Path.Combine(appDataDirectory, AppConstants.SecretsFileName)),
            new("Diagnostics.HistoryFile", Path.Combine(appDataDirectory, AppConstants.CaptureHistoryFileName)),
            new("Diagnostics.AppBaseDirectory", appBaseDirectory, Directory.Exists(appBaseDirectory)),
            new("Diagnostics.LocalesDirectory", localesDirectory, Directory.Exists(localesDirectory)),
            new("Diagnostics.TessdataDirectory", tessdataDirectory, Directory.Exists(tessdataDirectory)),
            new("Diagnostics.OcrLanguage", _settings.OcrLanguage),
            new("Diagnostics.OcrTrainedData", trainedDataPath, File.Exists(trainedDataPath)),
            new("Diagnostics.TesseractManagedAssembly", tesseractManagedPath, File.Exists(tesseractManagedPath)),
            new("Diagnostics.TesseractNativeAssembly", tesseractNativePath, File.Exists(tesseractNativePath)),
            new("Diagnostics.LeptonicaNativeAssembly", leptonicaNativePath, File.Exists(leptonicaNativePath)),
        ];
    }

    /// <summary>Gets the local application data directory used by Scrybe.</summary>
    public string GetAppDataDirectory()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, AppConstants.AppName);
    }

    /// <summary>Gets the directory where Scrybe writes local logs.</summary>
    public string GetLogsDirectory() => Path.Combine(GetAppDataDirectory(), AppConstants.LogSubDirName);
}
