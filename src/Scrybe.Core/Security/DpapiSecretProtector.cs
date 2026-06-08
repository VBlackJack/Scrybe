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

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Scrybe.Core.Interfaces;

namespace Scrybe.Core.Security;

/// <summary>
/// DPAPI CurrentUser secret protector. It avoids clipboard use entirely and clears managed byte
/// buffers plus DPAPI plaintext blobs immediately after converting to the short-lived string needed
/// by the keystroke injector.
/// </summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    private const int CryptProtectUiForbidden = 0x1;

    /// <inheritdoc />
    public string Protect(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        EnsureWindows();

        byte[] plaintext = Encoding.UTF8.GetBytes(secret);
        DataBlob input = CreateBlob(plaintext);
        DataBlob output = default;

        try
        {
            if (!CryptProtectData(
                    ref input,
                    AppConstants.AppName,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out output))
            {
                throw CreateCryptographicException("DPAPI failed to protect the secret.");
            }

            byte[] protectedBytes = CopyBlob(output);
            try
            {
                return Convert.ToBase64String(protectedBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            ZeroAndFreeHGlobal(input);
            FreeLocal(output);
        }
    }

    /// <inheritdoc />
    public char[] UnprotectToChars(string protectedSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);
        EnsureWindows();

        byte[] protectedBytes = Convert.FromBase64String(protectedSecret);
        byte[]? plaintext = null;
        DataBlob input = CreateBlob(protectedBytes);
        DataBlob output = default;
        IntPtr description = IntPtr.Zero;

        try
        {
            if (!CryptUnprotectData(
                    ref input,
                    out description,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out output))
            {
                throw CreateCryptographicException("DPAPI failed to unprotect the secret.");
            }

            plaintext = CopyBlob(output);
            return Encoding.UTF8.GetChars(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            ZeroAndFreeHGlobal(input);
            ZeroAndFreeLocal(output);
            FreeLocal(description);
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI secret protection is available only on Windows.");
        }
    }

    private static DataBlob CreateBlob(byte[] bytes)
    {
        IntPtr buffer = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, buffer, bytes.Length);
        return new DataBlob(bytes.Length, buffer);
    }

    private static byte[] CopyBlob(DataBlob blob)
    {
        byte[] bytes = new byte[blob.CbData];
        Marshal.Copy(blob.PbData, bytes, 0, bytes.Length);
        return bytes;
    }

    private static void ZeroAndFreeHGlobal(DataBlob blob)
    {
        if (blob.PbData == IntPtr.Zero)
        {
            return;
        }

        NativeSecureMemory.ZeroUnmanaged(blob.PbData, blob.CbData);
        Marshal.FreeHGlobal(blob.PbData);
    }

    private static void ZeroAndFreeLocal(DataBlob blob)
    {
        if (blob.PbData == IntPtr.Zero)
        {
            return;
        }

        NativeSecureMemory.ZeroUnmanaged(blob.PbData, blob.CbData);
        FreeLocal(blob.PbData);
    }

    private static void FreeLocal(DataBlob blob) => FreeLocal(blob.PbData);

    private static void FreeLocal(IntPtr buffer)
    {
        if (buffer != IntPtr.Zero)
        {
            LocalFree(buffer);
        }
    }

    private static CryptographicException CreateCryptographicException(string message)
    {
        int error = Marshal.GetLastPInvokeError();
        return new CryptographicException(message, new Win32Exception(error));
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? dataDescription,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        out IntPtr dataDescription,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public DataBlob(int length, IntPtr buffer)
        {
            CbData = length;
            PbData = buffer;
        }

        public int CbData;

        public IntPtr PbData;
    }
}
