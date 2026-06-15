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

/// <summary>Lets view models request a local directory through the host UI.</summary>
public interface IDirectoryPicker
{
    /// <summary>Prompts the user to pick a local directory.</summary>
    /// <param name="initialDirectory">Initial directory to show when available.</param>
    /// <param name="directory">Selected directory.</param>
    /// <returns><see langword="true"/> when the user selected a directory.</returns>
    bool TryPickDirectory(string? initialDirectory, out string directory);
}
