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

/// <summary>Prompts the user for target confirmation before password-grade injection.</summary>
public interface ITargetConfirmationPrompt
{
    /// <summary>Shows the native confirmation prompt.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Dialog body.</param>
    /// <returns><see langword="true"/> when the user confirms.</returns>
    bool Confirm(string title, string message);

    /// <summary>Shows the native target-unavailable warning.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Dialog body.</param>
    void ShowTargetUnavailable(string title, string message);
}
