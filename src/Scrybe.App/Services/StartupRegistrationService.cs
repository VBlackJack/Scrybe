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
using Microsoft.Win32;
using Scrybe.Core.Logging;
using Scrybe.Core.Startup;

namespace Scrybe.App.Services;

/// <summary>
/// Registry-backed <see cref="IStartupRegistration"/> over the per-user Run key (HKCU). Every access
/// goes through the named key and value constants; the value stores the quoted path to the current
/// executable so paths containing spaces survive.
/// </summary>
public sealed class StartupRegistrationService : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Scrybe";

    /// <inheritdoc />
    public bool IsEnabled() => GetRegisteredCommand() is not null;

    /// <inheritdoc />
    public string? GetRegisteredCommand()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) as string;
    }

    /// <inheritdoc />
    public void Enable()
    {
        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The current process path is unavailable.");

        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, StartupCommand.Format(executablePath), RegistryValueKind.String);
        FileLogger.Info("Autostart entry enabled for the current executable.");
    }

    /// <inheritdoc />
    public void Disable()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
        FileLogger.Info("Autostart entry disabled.");
    }

    /// <inheritdoc />
    public void HealIfStale()
    {
        try
        {
            string? command = GetRegisteredCommand();
            if (command is null)
            {
                return;
            }

            if (!StartupCommand.TryExtractPath(command, out string registeredPath))
            {
                // A non-empty command we cannot parse belongs to another tool: leave it untouched.
                return;
            }

            if (!File.Exists(registeredPath))
            {
                Enable();
                FileLogger.Info("Autostart entry healed: re-registered the current executable over a missing path.");
            }
        }
        catch (Exception exception)
        {
            FileLogger.Error("Failed to heal the autostart entry.", exception);
        }
    }
}
