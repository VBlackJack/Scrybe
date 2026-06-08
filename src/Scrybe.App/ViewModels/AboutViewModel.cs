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

using Scrybe.App.Services;
using Scrybe.Core.Interfaces;

namespace Scrybe.App.ViewModels;

/// <summary>View model for the About window.</summary>
public sealed class AboutViewModel
{
    /// <summary>Initializes the About view model from localization and assembly metadata.</summary>
    public AboutViewModel(ILocalizationManager localization, AboutInfoProvider aboutInfoProvider)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(aboutInfoProvider);

        AboutInfo aboutInfo = aboutInfoProvider.GetAboutInfo();

        Title = localization["About.Title"];
        AppName = localization["AppTitle"];
        Tagline = localization["AppTagline"];
        Version = aboutInfo.Version;
        Rows =
        [
            new AboutDetailRow(localization["About.Version"], aboutInfo.Version),
            new AboutDetailRow(localization["About.BuildDate"], aboutInfo.BuildDate),
            new AboutDetailRow(localization["About.Commit"], aboutInfo.CommitHash),
            new AboutDetailRow(localization["About.License"], aboutInfo.License),
            new AboutDetailRow(localization["About.Author"], aboutInfo.Author),
            new AboutDetailRow(localization["About.Copyright"], aboutInfo.Copyright),
        ];
    }

    /// <summary>Localized window title.</summary>
    public string Title { get; }

    /// <summary>Localized application name.</summary>
    public string AppName { get; }

    /// <summary>Localized tagline.</summary>
    public string Tagline { get; }

    /// <summary>Display version shown in the header.</summary>
    public string Version { get; }

    /// <summary>Localized detail rows.</summary>
    public IReadOnlyList<AboutDetailRow> Rows { get; }
}
