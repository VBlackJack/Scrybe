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

/// <summary>Gateway for OS-bound target-window operations used before keystroke injection.</summary>
public interface ITargetWindowGateway
{
    /// <summary>Returns the current foreground window handle.</summary>
    IntPtr GetForegroundWindow();

    /// <summary>Gets the input layout of the thread owning a target window.</summary>
    /// <param name="window">Target window.</param>
    IntPtr GetKeyboardLayout(IntPtr window);

    /// <summary>Resolves metadata for <paramref name="window"/> when it is still available.</summary>
    /// <param name="window">Captured target window handle.</param>
    /// <param name="target">Resolved target metadata.</param>
    bool TryGetInfo(IntPtr window, out TargetWindowInfo? target);

    /// <summary>Attempts to restore focus to <paramref name="window"/>.</summary>
    /// <param name="window">Captured target window handle.</param>
    bool TryRestore(IntPtr window);
}
