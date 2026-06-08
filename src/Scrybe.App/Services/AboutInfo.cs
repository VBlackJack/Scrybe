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

namespace Scrybe.App.Services;

/// <summary>Display-ready application metadata for the About window.</summary>
public sealed class AboutInfo
{
    /// <summary>Initializes display-ready application metadata.</summary>
    public AboutInfo(
        string version,
        string buildDate,
        string commitHash,
        string license,
        string author,
        string copyright)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildDate);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(license);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentException.ThrowIfNullOrWhiteSpace(copyright);

        Version = version;
        BuildDate = buildDate;
        CommitHash = commitHash;
        License = license;
        Author = author;
        Copyright = copyright;
    }

    /// <summary>Display version stripped of build metadata.</summary>
    public string Version { get; }

    /// <summary>Build date or a neutral placeholder.</summary>
    public string BuildDate { get; }

    /// <summary>Short commit hash or a neutral dev marker.</summary>
    public string CommitHash { get; }

    /// <summary>License display value.</summary>
    public string License { get; }

    /// <summary>Author display value.</summary>
    public string Author { get; }

    /// <summary>Copyright display value.</summary>
    public string Copyright { get; }
}
