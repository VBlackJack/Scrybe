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

using Scrybe.Core.Interfaces;

namespace Scrybe.App.Services;

/// <summary>Confirms and restores the foreground target before password-grade injection.</summary>
public interface IInjectionTargetConfirmer
{
    /// <summary>
    /// Confirms the captured target with the user and restores it before injection.
    /// </summary>
    /// <param name="target">The target window captured before any Scrybe UI was shown.</param>
    /// <param name="confirmTitle">Dialog title.</param>
    /// <param name="confirmMessageTemplate">Message template receiving target details.</param>
    /// <param name="targetUnavailableMessage">Message shown when the target is unavailable.</param>
    /// <param name="untitledTargetText">Fallback title for untitled windows.</param>
    /// <returns><see langword="true"/> when the target was confirmed and restored.</returns>
    bool TryConfirmAndRestore(
        IntPtr target,
        string confirmTitle,
        string confirmMessageTemplate,
        string targetUnavailableMessage,
        string untitledTargetText, out IInjectionContext? context);
}
