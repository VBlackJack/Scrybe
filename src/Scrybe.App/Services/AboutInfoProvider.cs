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

using System.Reflection;

namespace Scrybe.App.Services;

/// <summary>Reads application metadata from the executing assembly.</summary>
public sealed class AboutInfoProvider
{
    private const string BuildDateMetadataName = "BuildDate";
    private const string CommitHashMetadataName = "CommitHash";
    private const string UnknownValue = "-";
    private const string DevCommitValue = "dev";
    private const string LicenseValue = "Apache License 2.0";
    private const string CopyrightValue = "Copyright 2026 Julien Bombled";
    private const int ShortCommitLength = 7;

    /// <summary>Gets display-ready metadata for the About window.</summary>
    public AboutInfo GetAboutInfo()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        string version = ResolveVersion(assembly, informationalVersion);
        string buildDate = ResolveMetadataValue(assembly, BuildDateMetadataName, UnknownValue);
        string commitHash = ResolveCommitHash(assembly, informationalVersion);

        return new AboutInfo(version, buildDate, commitHash, LicenseValue, CopyrightValue);
    }

    private static string ResolveVersion(Assembly assembly, string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return assembly.GetName().Version?.ToString() ?? "0.0.0";
        }

        int metadataSeparator = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        return metadataSeparator >= 0 ? informationalVersion[..metadataSeparator] : informationalVersion;
    }

    private static string ResolveCommitHash(Assembly assembly, string? informationalVersion)
    {
        string metadataCommit = ResolveMetadataValue(assembly, CommitHashMetadataName, string.Empty);
        if (!string.IsNullOrWhiteSpace(metadataCommit))
        {
            return metadataCommit;
        }

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            int metadataSeparator = informationalVersion.IndexOf('+', StringComparison.Ordinal);
            if (metadataSeparator >= 0 && metadataSeparator < informationalVersion.Length - 1)
            {
                string suffix = informationalVersion[(metadataSeparator + 1)..].Trim();
                return ShortenCommitHash(suffix);
            }
        }

        return DevCommitValue;
    }

    private static string ShortenCommitHash(string commitHash)
    {
        if (commitHash.Length <= ShortCommitLength)
        {
            return commitHash;
        }

        return commitHash[..ShortCommitLength];
    }

    private static string ResolveMetadataValue(Assembly assembly, string name, string fallback)
    {
        foreach (AssemblyMetadataAttribute attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (string.Equals(attribute.Key, name, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(attribute.Value))
            {
                return attribute.Value;
            }
        }

        return fallback;
    }
}
