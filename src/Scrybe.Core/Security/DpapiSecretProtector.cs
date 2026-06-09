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
/// buffers plus DPAPI plaintext blobs immediately after converting to the short-lived buffer needed
/// by the keystroke injector.
/// </summary>
/// <remarks>
/// Scrybe adds stable application-specific DPAPI optional entropy as defense in depth. This does
/// not create a cryptographic boundary against a process that can reverse-engineer Scrybe in the
/// same user session, but it does bind the vault to Scrybe's scheme and defeats generic DPAPI sweeps.
/// </remarks>
public sealed class DpapiSecretProtector : IMigratingSecretProtector
{
    private const int CryptProtectUiForbidden = 0x1;
    private const byte CurrentProtectedValueVersion = 1;
    private const string EntropyMaterial = "Scrybe secret vault DPAPI optional entropy v1 | 2026-06-09 | Apache-2.0";

    /// <inheritdoc />
    public string Protect(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return Protect(secret.AsSpan());
    }

    /// <inheritdoc />
    public bool IsProtectedWithCurrentScheme(string protectedSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);

        byte[] protectedValue = Convert.FromBase64String(protectedSecret);
        try
        {
            return HasCurrentSchemeHeader(protectedValue);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedValue);
        }
    }

    /// <inheritdoc />
    public string ReprotectWithCurrentScheme(string protectedSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);

        if (IsProtectedWithCurrentScheme(protectedSecret))
        {
            return protectedSecret;
        }

        char[]? plaintext = null;
        try
        {
            plaintext = UnprotectToChars(protectedSecret);
            return Protect(plaintext.AsSpan());
        }
        finally
        {
            SecretMemory.Clear(plaintext);
        }
    }

    private static string Protect(ReadOnlySpan<char> secret)
    {
        EnsureWindows();

        int byteCount = Encoding.UTF8.GetByteCount(secret);
        byte[] plaintext = new byte[byteCount];
        Encoding.UTF8.GetBytes(secret, plaintext);

        DataBlob input = CreateBlob(plaintext);
        DataBlob entropy = CreateEntropyBlob();
        DataBlob output = default;
        byte[]? protectedBytes = null;
        byte[]? protectedValue = null;

        try
        {
            if (!CryptProtectDataWithEntropy(
                    ref input,
                    AppConstants.AppName,
                    ref entropy,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out output))
            {
                throw CreateCryptographicException("DPAPI failed to protect the secret.");
            }

            protectedBytes = CopyBlob(output);
            protectedValue = AddCurrentSchemeHeader(protectedBytes);
            return Convert.ToBase64String(protectedValue);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }

            if (protectedValue is not null)
            {
                CryptographicOperations.ZeroMemory(protectedValue);
            }

            ZeroAndFreeHGlobal(input);
            ZeroAndFreeHGlobal(entropy);
            FreeLocal(output);
        }
    }

    /// <inheritdoc />
    public char[] UnprotectToChars(string protectedSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);
        EnsureWindows();

        byte[] protectedValue = Convert.FromBase64String(protectedSecret);
        byte[]? plaintext = null;
        bool useEntropy = TryGetCurrentSchemePayload(protectedValue, out int payloadOffset, out int payloadLength);
        DataBlob input = CreateBlob(protectedValue, payloadOffset, payloadLength);
        DataBlob entropy = useEntropy ? CreateEntropyBlob() : default;
        DataBlob output = default;
        IntPtr description = IntPtr.Zero;

        try
        {
            bool unprotected = useEntropy
                ? CryptUnprotectDataWithEntropy(
                    ref input,
                    out description,
                    ref entropy,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out output)
                : CryptUnprotectData(
                    ref input,
                    out description,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out output);

            if (!unprotected)
            {
                throw CreateCryptographicException("DPAPI failed to unprotect the secret.");
            }

            plaintext = CopyBlob(output);
            return Encoding.UTF8.GetChars(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedValue);
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            ZeroAndFreeHGlobal(input);
            ZeroAndFreeHGlobal(entropy);
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

    private static DataBlob CreateBlob(byte[] bytes) => CreateBlob(bytes, offset: 0, bytes.Length);

    private static DataBlob CreateBlob(byte[] bytes, int offset, int length)
    {
        IntPtr buffer = Marshal.AllocHGlobal(length);
        if (length > 0)
        {
            Marshal.Copy(bytes, offset, buffer, length);
        }

        return new DataBlob(length, buffer);
    }

    private static DataBlob CreateEntropyBlob()
    {
        byte[] entropy = Encoding.UTF8.GetBytes(EntropyMaterial);
        try
        {
            return CreateBlob(entropy);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(entropy);
        }
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

    private static byte[] AddCurrentSchemeHeader(byte[] protectedBytes)
    {
        ReadOnlySpan<byte> magic = CurrentSchemeMagic;
        byte[] protectedValue = new byte[magic.Length + 1 + protectedBytes.Length];
        magic.CopyTo(protectedValue);
        protectedValue[magic.Length] = CurrentProtectedValueVersion;
        Buffer.BlockCopy(protectedBytes, 0, protectedValue, magic.Length + 1, protectedBytes.Length);
        return protectedValue;
    }

    private static bool TryGetCurrentSchemePayload(byte[] protectedValue, out int payloadOffset, out int payloadLength)
    {
        if (HasCurrentSchemeHeader(protectedValue))
        {
            payloadOffset = CurrentSchemeMagic.Length + 1;
            payloadLength = protectedValue.Length - payloadOffset;
            return true;
        }

        payloadOffset = 0;
        payloadLength = protectedValue.Length;
        return false;
    }

    private static bool HasCurrentSchemeHeader(ReadOnlySpan<byte> protectedValue)
    {
        ReadOnlySpan<byte> magic = CurrentSchemeMagic;
        return protectedValue.Length >= magic.Length + 1
            && protectedValue[..magic.Length].SequenceEqual(magic)
            && protectedValue[magic.Length] == CurrentProtectedValueVersion;
    }

    private static ReadOnlySpan<byte> CurrentSchemeMagic =>
    [
        0x53, // S
        0x43, // C
        0x52, // R
        0x59, // Y
        0x42, // B
        0x45, // E
        0x44, // D
        0x50, // P
        0x41, // A
        0x50, // P
        0x49, // I
    ];

    private static CryptographicException CreateCryptographicException(string message)
    {
        int error = Marshal.GetLastPInvokeError();
        return new CryptographicException(message, new Win32Exception(error));
    }

    [DllImport("crypt32.dll", EntryPoint = "CryptProtectData", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectDataWithEntropy(
        ref DataBlob dataIn,
        string? dataDescription,
        ref DataBlob optionalEntropy,
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

    [DllImport("crypt32.dll", EntryPoint = "CryptUnprotectData", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectDataWithEntropy(
        ref DataBlob dataIn,
        out IntPtr dataDescription,
        ref DataBlob optionalEntropy,
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
