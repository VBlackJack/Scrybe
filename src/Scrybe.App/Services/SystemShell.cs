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

using System.Diagnostics;
using System.IO;
using Scrybe.Core.Logging;

namespace Scrybe.App.Services;

/// <summary>Windows shell adapter used by view models for local troubleshooting actions.</summary>
public sealed class SystemShell : ISystemShell
{
    /// <inheritdoc />
    public bool TryOpenDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(directory);
            using Process? _ = Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception exception)
        {
            FileLogger.Error($"Failed to open directory '{directory}'.", exception);
            return false;
        }
    }
}
