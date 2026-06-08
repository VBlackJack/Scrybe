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

using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Scrybe.Core.Security;

/// <summary>Small wrapper for best-effort unmanaged memory zeroing before buffers are released.</summary>
internal static class NativeSecureMemory
{
    private const int ChunkSize = 4096;

    /// <summary>Zeros an unmanaged buffer before it is released.</summary>
    /// <param name="buffer">The unmanaged buffer.</param>
    /// <param name="length">The number of bytes to clear.</param>
    public static void ZeroUnmanaged(IntPtr buffer, int length)
    {
        if (buffer == IntPtr.Zero || length <= 0)
        {
            return;
        }

        byte[] zeros = new byte[Math.Min(length, ChunkSize)];

        try
        {
            int offset = 0;
            while (offset < length)
            {
                int count = Math.Min(zeros.Length, length - offset);
                Marshal.Copy(zeros, 0, IntPtr.Add(buffer, offset), count);
                offset += count;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(zeros);
        }
    }
}
