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

using FluentAssertions;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Scrybe.App.Services;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Scrybe.Core.Secrets;
using Scrybe.Core.Security;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for DPAPI secret protection and the encrypted JSON vault store.</summary>
public sealed class SecretVaultTests
{
    private const string PlainSecret = "ScrybeSecret#2026!";
    private const int CryptProtectUiForbidden = 0x1;

    [Fact]
    public void DpapiProtector_RoundTrip_RestoresSecretWithoutPlainCiphertext()
    {
        IMigratingSecretProtector protector = new DpapiSecretProtector();

        string protectedSecret = protector.Protect(PlainSecret);
        char[] restored = protector.UnprotectToChars(protectedSecret);

        try
        {
            protector.IsProtectedWithCurrentScheme(protectedSecret).Should().BeTrue();
            restored.Should().Equal(PlainSecret.ToCharArray());
            protectedSecret.Should().NotContain(PlainSecret);
        }
        finally
        {
            SecretMemory.Clear(restored);
        }

        restored.Should().OnlyContain(character => character == '\0');
    }

    [Fact]
    public void DpapiProtector_LegacyBlob_StillDecrypts()
    {
        IMigratingSecretProtector protector = new DpapiSecretProtector();
        string legacyProtectedSecret = ProtectLegacyWithoutEntropy(PlainSecret);

        char[] restored = protector.UnprotectToChars(legacyProtectedSecret);

        try
        {
            protector.IsProtectedWithCurrentScheme(legacyProtectedSecret).Should().BeFalse();
            restored.Should().Equal(PlainSecret.ToCharArray());
        }
        finally
        {
            SecretMemory.Clear(restored);
        }
    }

    [Fact]
    public async Task SecretLibrary_LoadAsync_MigratesLegacySecretsAndPersistsOnce()
    {
        IMigratingSecretProtector protector = new DpapiSecretProtector();
        string legacyProtectedSecret = ProtectLegacyWithoutEntropy(PlainSecret);
        InMemorySecretStore store = new();
        SecretEntry legacyEntry = new(
            "secret-1",
            "Lab password",
            "administrator",
            legacyProtectedSecret,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(-1));
        store.SavedSecrets = [legacyEntry];
        SecretLibrary library = new(store, protector);

        await library.LoadAsync();

        store.SaveCount.Should().Be(1);
        store.SavedSecrets.Should().ContainSingle();
        SecretEntry migrated = store.SavedSecrets[0];
        migrated.ProtectedSecret.Should().NotBe(legacyProtectedSecret);
        protector.IsProtectedWithCurrentScheme(migrated.ProtectedSecret).Should().BeTrue();
        migrated.CreatedAtUtc.Should().Be(legacyEntry.CreatedAtUtc);
        migrated.UpdatedAtUtc.Should().Be(legacyEntry.UpdatedAtUtc);
        char[] restored = protector.UnprotectToChars(migrated.ProtectedSecret);
        try
        {
            restored.Should().Equal(PlainSecret.ToCharArray());
        }
        finally
        {
            SecretMemory.Clear(restored);
        }

        await library.LoadAsync();

        store.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task SecretLibrary_LoadAsync_DoesNotRewriteCurrentSchemeSecrets()
    {
        IMigratingSecretProtector protector = new DpapiSecretProtector();
        InMemorySecretStore store = new();
        SecretEntry currentEntry = new(
            "secret-1",
            "Lab password",
            "administrator",
            protector.Protect(PlainSecret),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        store.SavedSecrets = [currentEntry];
        SecretLibrary library = new(store, protector);

        await library.LoadAsync();

        store.SaveCount.Should().Be(0);
        library.Secrets.Should().ContainSingle().Which.ProtectedSecret.Should().Be(currentEntry.ProtectedSecret);
    }

    [Fact]
    public async Task JsonSecretStore_RoundTrip_DoesNotPersistPlaintext()
    {
        string path = CreateTempPath();
        try
        {
            ISecretProtector protector = new DpapiSecretProtector();
            ISecretStore store = new JsonSecretStore(path);
            SecretEntry saved = new(
                "secret-1",
                "Lab password",
                "administrator",
                protector.Protect(PlainSecret),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);

            await store.SaveAsync([saved]);
            string rawJson = await File.ReadAllTextAsync(path);
            IReadOnlyList<SecretEntry> loaded = await store.LoadAsync();

            rawJson.Should().NotContain(PlainSecret);
            loaded.Should().ContainSingle();
            char[] restored = protector.UnprotectToChars(loaded[0].ProtectedSecret);
            try
            {
                restored.Should().Equal(PlainSecret.ToCharArray());
            }
            finally
            {
                SecretMemory.Clear(restored);
            }
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task JsonSecretStore_CorruptFile_LoadsEmptyWithoutThrowing()
    {
        string path = CreateTempPath();
        await File.WriteAllTextAsync(path, "{ not valid secrets json ]");
        try
        {
            ISecretStore store = new JsonSecretStore(path);

            IReadOnlyList<SecretEntry> loaded = await store.LoadAsync();

            loaded.Should().BeEmpty();
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static string CreateTempPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ScrybeSecretTests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
    }

    private static string ProtectLegacyWithoutEntropy(string secret)
    {
        byte[] plaintext = Encoding.UTF8.GetBytes(secret);
        DataBlob input = CreateBlob(plaintext);
        DataBlob output = default;
        byte[]? protectedBytes = null;

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
                throw CreateCryptographicException("DPAPI failed to protect the legacy test secret.");
            }

            protectedBytes = CopyBlob(output);
            return Convert.ToBase64String(protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }

            ZeroAndFreeHGlobal(input);
            ZeroAndFreeLocal(output);
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

        ZeroUnmanaged(blob.PbData, blob.CbData);
        Marshal.FreeHGlobal(blob.PbData);
    }

    private static void ZeroAndFreeLocal(DataBlob blob)
    {
        if (blob.PbData == IntPtr.Zero)
        {
            return;
        }

        ZeroUnmanaged(blob.PbData, blob.CbData);
        LocalFree(blob.PbData);
    }

    private static void ZeroUnmanaged(IntPtr buffer, int length)
    {
        if (length <= 0)
        {
            return;
        }

        byte[] zeroes = new byte[length];
        Marshal.Copy(zeroes, 0, buffer, length);
    }

    private static CryptographicException CreateCryptographicException(string message)
    {
        int error = Marshal.GetLastPInvokeError();
        return new CryptographicException(message, new Win32Exception(error));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        public List<SecretEntry> SavedSecrets { get; set; } = [];

        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<SecretEntry>> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SecretEntry>>(SavedSecrets);

        public Task SaveAsync(IReadOnlyList<SecretEntry> secrets, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            SavedSecrets = [.. secrets];
            return Task.CompletedTask;
        }
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
