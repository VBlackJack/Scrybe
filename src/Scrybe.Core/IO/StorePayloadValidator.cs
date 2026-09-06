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

using System.Text.Json;
using Scrybe.Core.Models;
using Scrybe.Core.Security;
using Scrybe.Core.Snippets;

namespace Scrybe.Core.IO;

/// <summary>Validates restored store structure before bytes may replace a live file.</summary>
public static class StorePayloadValidator
{
    /// <summary>Returns the number of records (one for settings), rejecting malformed payloads.</summary>
    public static int Validate(string name, byte[] bytes)
    {
        if (name == AppConstants.SettingsFileName)
        {
            AppSettings value = JsonSerializer.Deserialize<AppSettings>(bytes) ?? throw new InvalidDataException("Missing settings.");
            if (value.InjectionProfiles is null || value.InjectionProfiles.Any(profile => profile is null || !profile.IsValid)
                || value.InjectionProfiles.Select(profile => profile.ProcessName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != value.InjectionProfiles.Count)
            { throw new InvalidDataException("Invalid profiles."); }
            return 1;
        }
        if (name == AppConstants.SnippetsFileName)
        {
            List<Snippet> entries = JsonSerializer.Deserialize<List<Snippet>>(bytes) ?? throw new InvalidDataException("Missing snippets.");
            SnippetExchange.Validate(entries);
            return entries.Count;
        }
        if (name == AppConstants.SecretsFileName)
        {
            List<SecretEntry> entries = JsonSerializer.Deserialize<List<SecretEntry>>(bytes) ?? throw new InvalidDataException("Missing secrets.");
            if (entries.Any(entry => entry is null || string.IsNullOrWhiteSpace(entry.Id)
                || string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.ProtectedSecret))
                || entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() != entries.Count)
            { throw new InvalidDataException("Invalid secret metadata."); }
            foreach (SecretEntry entry in entries) { ValidateProtected(entry.ProtectedSecret); }
            return entries.Count;
        }
        if (name == AppConstants.CaptureHistoryFileName)
        {
            List<CaptureHistoryEntry> entries = JsonSerializer.Deserialize<List<CaptureHistoryEntry>>(bytes) ?? throw new InvalidDataException("Missing history.");
            if (entries.Any(entry => entry is null || string.IsNullOrWhiteSpace(entry.Id)
                || string.IsNullOrWhiteSpace(entry.ProtectedText) || entry.CharCount < 0)
                || entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() != entries.Count)
            { throw new InvalidDataException("Invalid history metadata."); }
            foreach (CaptureHistoryEntry entry in entries) { ValidateProtected(entry.ProtectedText); }
            return entries.Count;
        }
        throw new InvalidDataException("Unsupported store.");
    }

    private static void ValidateProtected(string ciphertext)
    {
        char[]? plaintext = null;
        try { plaintext = new DpapiSecretProtector().UnprotectToChars(ciphertext); }
        finally { SecretMemory.Clear(plaintext); }
    }
}
