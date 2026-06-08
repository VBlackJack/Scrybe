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
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;

namespace Scrybe.Core.Localization;

/// <summary>
/// Loads flat <c>{ "key": "value" }</c> JSON locale files from a directory and exposes
/// their entries through an indexer. Unknown keys return the key itself so missing
/// translations are visible rather than blank.
/// </summary>
public sealed class LocalizationManager : ILocalizationManager
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    private readonly string _localesDirectory;
    private IReadOnlyDictionary<string, string> _strings = new Dictionary<string, string>();
    private string _current = AppConstants.DefaultLocaleCode;

    /// <summary>Initializes a new instance backed by JSON files in <paramref name="localesDirectory"/>.</summary>
    /// <param name="localesDirectory">Directory containing the <c>{code}.json</c> locale files.</param>
    public LocalizationManager(string localesDirectory)
    {
        _localesDirectory = localesDirectory;
    }

    /// <inheritdoc />
    public string this[string key]
    {
        get
        {
            if (_strings.TryGetValue(key, out string? value) && value is not null)
            {
                return value;
            }

            return key;
        }
    }

    /// <inheritdoc />
    public string Current => _current;

    /// <inheritdoc />
    public event EventHandler? LocaleChanged;

    /// <inheritdoc />
    public async Task LoadAsync(string localeCode, CancellationToken cancellationToken = default)
    {
        string filePath = Path.Combine(_localesDirectory, localeCode + AppConstants.LocaleFileExtension);

        if (!File.Exists(filePath))
        {
            FileLogger.Warn($"Locale file not found: {filePath}; keeping current locale '{_current}'.");
            return;
        }

        try
        {
            await using FileStream stream = File.OpenRead(filePath);
            Dictionary<string, string>? parsed = await JsonSerializer
                .DeserializeAsync<Dictionary<string, string>>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            _strings = parsed ?? new Dictionary<string, string>();
            _current = localeCode;
            FileLogger.Info($"Locale loaded: '{localeCode}' ({_strings.Count} keys).");
            LocaleChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            FileLogger.Error($"Failed to load locale '{localeCode}' from {filePath}.", exception);
        }
    }
}
