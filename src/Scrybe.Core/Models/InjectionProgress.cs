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

namespace Scrybe.Core.Models;

/// <summary>Non-sensitive progress: logical strokes processed, not confirmed target reception.</summary>
public sealed record InjectionProgress(string Target, int Completed, int Total, bool IsRunning, string StatusKey);

/// <summary>Machine-readable reason for an interrupted injection.</summary>
public enum InjectionFailureReason { None, Cancelled, TargetChanged, Unmappable, NativeFailure, HigherIntegrity }
