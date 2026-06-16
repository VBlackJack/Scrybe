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
using System.Security.AccessControl;
using System.Security.Principal;
using Scrybe.Core.Logging;

namespace Scrybe.App.Services;

/// <summary>Applies a private per-user ACL to the local application data directory.</summary>
public static class AppDataSecurity
{
    private static readonly EnumerationOptions RecursiveEnumerationOptions = new()
    {
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
    };

    /// <summary>
    /// Ensures <paramref name="directory"/> exists and restricts its ACL to the current user,
    /// administrators and LocalSystem.
    /// </summary>
    /// <param name="directory">Per-user application data directory to harden.</param>
    public static void EnsurePrivateDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception exception)
        {
            FileLogger.Warn($"Failed to create local app data directory '{directory}': {Describe(exception)}");
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        SecurityIdentifier[] allowedSids;
        try
        {
            allowedSids = BuildAllowedSids();
        }
        catch (Exception exception)
        {
            FileLogger.Warn($"Failed to resolve Windows identities for local app data ACLs: {Describe(exception)}");
            return;
        }

        ApplyDirectoryAclSafely(directory, allowedSids);

        foreach (string childDirectory in EnumerateDirectories(directory))
        {
            ApplyDirectoryAclSafely(childDirectory, allowedSids);
        }

        foreach (string file in EnumerateFiles(directory))
        {
            ApplyFileAclSafely(file, allowedSids);
        }
    }

    private static SecurityIdentifier[] BuildAllowedSids()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return
        [
            identity.User ?? throw new InvalidOperationException("The current Windows identity has no user SID."),
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, domainSid: null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, domainSid: null),
        ];
    }

    private static string[] EnumerateDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory, "*", RecursiveEnumerationOptions).ToArray();
        }
        catch (Exception exception)
        {
            FileLogger.Warn($"Failed to enumerate local app data directories under '{directory}': {Describe(exception)}");
            return [];
        }
    }

    private static string[] EnumerateFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*", RecursiveEnumerationOptions).ToArray();
        }
        catch (Exception exception)
        {
            FileLogger.Warn($"Failed to enumerate local app data files under '{directory}': {Describe(exception)}");
            return [];
        }
    }

    private static void ApplyDirectoryAclSafely(string directory, IReadOnlyList<SecurityIdentifier> allowedSids)
    {
        try
        {
            DirectoryInfo info = new(directory);
            DirectorySecurity security = info.GetAccessControl(AccessControlSections.Access);
            ReplaceAccessRules(
                security,
                allowedSids,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit);
            info.SetAccessControl(security);
        }
        catch (Exception exception)
        {
            FileLogger.Warn($"Failed to harden local app data directory ACL '{directory}': {Describe(exception)}");
        }
    }

    private static void ApplyFileAclSafely(string filePath, IReadOnlyList<SecurityIdentifier> allowedSids)
    {
        try
        {
            FileInfo info = new(filePath);
            FileSecurity security = info.GetAccessControl(AccessControlSections.Access);
            ReplaceAccessRules(security, allowedSids, InheritanceFlags.None);
            info.SetAccessControl(security);
        }
        catch (Exception exception)
        {
            FileLogger.Warn($"Failed to harden local app data file ACL '{filePath}': {Describe(exception)}");
        }
    }

    private static void ReplaceAccessRules(
        FileSystemSecurity security,
        IReadOnlyList<SecurityIdentifier> allowedSids,
        InheritanceFlags inheritanceFlags)
    {
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        AuthorizationRuleCollection existingRules = security.GetAccessRules(
            includeExplicit: true,
            includeInherited: false,
            targetType: typeof(SecurityIdentifier));

        foreach (IdentityReference identity in existingRules
            .OfType<FileSystemAccessRule>()
            .Select(rule => rule.IdentityReference)
            .Distinct())
        {
            security.PurgeAccessRules(identity);
        }

        foreach (SecurityIdentifier sid in allowedSids)
        {
            security.AddAccessRule(new FileSystemAccessRule(
                sid,
                FileSystemRights.FullControl,
                inheritanceFlags,
                PropagationFlags.None,
                AccessControlType.Allow));
        }
    }

    private static string Describe(Exception exception)
        => $"{exception.GetType().Name}: {exception.Message}";
}
