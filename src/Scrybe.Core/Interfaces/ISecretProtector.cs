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

/// <summary>Protects and unprotects secret material for at-rest storage.</summary>
public interface ISecretProtector
{
    /// <summary>Protects <paramref name="secret"/> and returns a persistable ciphertext string.</summary>
    /// <param name="secret">The plaintext secret.</param>
    /// <returns>A base64 ciphertext string suitable for persistence.</returns>
    string Protect(string secret);

    /// <summary>Unprotects a previously persisted ciphertext string into a caller-owned character buffer.</summary>
    /// <param name="protectedSecret">The protected ciphertext string.</param>
    /// <returns>The plaintext secret characters, intended for immediate injection only.</returns>
    char[] UnprotectToChars(string protectedSecret);
}
