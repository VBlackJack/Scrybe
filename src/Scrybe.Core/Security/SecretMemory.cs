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

namespace Scrybe.Core.Security;

/// <summary>Best-effort clearing helpers for plaintext secret buffers owned by Scrybe.</summary>
public static class SecretMemory
{
    /// <summary>
    /// Clears a managed character buffer. This removes the copy Scrybe controls, but managed runtimes
    /// can move or copy memory, so this is not a perfect forensic guarantee.
    /// </summary>
    /// <param name="secret">The character buffer to clear.</param>
    public static void Clear(char[]? secret)
    {
        if (secret is null || secret.Length == 0)
        {
            return;
        }

        Array.Clear(secret, 0, secret.Length);
    }
}
