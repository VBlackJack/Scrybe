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

namespace Scrybe.Core.Interfaces;

/// <summary>Identity and input layout of a confirmed target, checked throughout paced typing.</summary>
public interface IInjectionContext
{
    /// <summary>Immutable injection settings captured before target confirmation.</summary>
    Scrybe.Core.Models.InjectionProfile? Profile => null;

    /// <summary>Whether the confirmed target still owns foreground focus and the same input layout.</summary>
    bool IsCurrent { get; }

    /// <summary>Non-sensitive identity displayed while typing; never includes input contents.</summary>
    string TargetDisplay => string.Empty;

    /// <summary>Reports processed logical strokes, distinct from native event counts.</summary>
    void ReportProgress(int completed, int total) { }

    /// <summary>The captured target thread's keyboard layout, never the worker thread's layout.</summary>
    IntPtr KeyboardLayout { get; }
}
