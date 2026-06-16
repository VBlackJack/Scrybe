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

using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using Scrybe.App.Services;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for the Windows ACL hardening applied to local app data.</summary>
public sealed class AppDataSecurityTests
{
    [Fact]
    public void EnsurePrivateDirectory_ProtectsDirectoryAndExistingFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ScrybeAppDataSecurityTests", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(directory, "settings.json");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(filePath, "{}");

            AppDataSecurity.EnsurePrivateDirectory(directory);

            DirectorySecurity directorySecurity = new DirectoryInfo(directory)
                .GetAccessControl(AccessControlSections.Access);
            FileSecurity fileSecurity = new FileInfo(filePath).GetAccessControl(AccessControlSections.Access);

            directorySecurity.AreAccessRulesProtected.Should().BeTrue();
            fileSecurity.AreAccessRulesProtected.Should().BeTrue();
            ShouldHaveCurrentUserFullControlRule(directorySecurity);
            ShouldHaveCurrentUserFullControlRule(fileSecurity);
        }
        finally
        {
            TryDelete(directory);
        }
    }

    private static void ShouldHaveCurrentUserFullControlRule(FileSystemSecurity security)
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier currentUser = identity.User
            ?? throw new InvalidOperationException("The current Windows identity has no user SID.");

        AuthorizationRuleCollection rules = security.GetAccessRules(
            includeExplicit: true,
            includeInherited: false,
            targetType: typeof(SecurityIdentifier));

        rules
            .OfType<FileSystemAccessRule>()
            .Should()
            .Contain(
                rule => currentUser.Equals(rule.IdentityReference)
                    && rule.AccessControlType == AccessControlType.Allow
                    && (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl);
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; a leftover temp directory must not fail the test run.
        }
    }
}
