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

/// <summary>
/// Optional capability for secret protectors that can identify and upgrade older persisted schemes.
/// </summary>
public interface IMigratingSecretProtector : ISecretProtector
{
    /// <summary>
    /// Returns whether <paramref name="protectedSecret"/> already uses the current protection scheme.
    /// </summary>
    /// <param name="protectedSecret">Persisted protected secret value.</param>
    bool IsProtectedWithCurrentScheme(string protectedSecret);

    /// <summary>
    /// Re-protects <paramref name="protectedSecret"/> with the current scheme while preserving plaintext.
    /// </summary>
    /// <param name="protectedSecret">Persisted protected secret value, possibly using an older scheme.</param>
    /// <returns>A protected value using the current scheme.</returns>
    string ReprotectWithCurrentScheme(string protectedSecret);
}
