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
using Scrybe.Core.Interfaces;
using Binding = System.Windows.Data.Binding;

namespace Scrybe.App.Localization;

/// <summary>
/// Singleton binding bridge over <see cref="ILocalizationManager"/>. XAML binds to its
/// string indexer through <see cref="TranslateExtension"/>; when the active locale changes
/// it raises a change notification for the indexer so every bound element updates live.
/// </summary>
public sealed class LocalizationSource : INotifyPropertyChanged
{
    private static readonly LocalizationSource SharedInstance = new();

    private ILocalizationManager? _manager;

    private LocalizationSource()
    {
    }

    /// <summary>Gets the shared application-wide instance.</summary>
    public static LocalizationSource Instance => SharedInstance;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Returns the translated value for <paramref name="key"/>, or the key itself when no
    /// localization manager is attached yet (for example at design time).
    /// </summary>
    /// <param name="key">The translation key to resolve.</param>
    public string this[string key] => _manager is null ? key : _manager[key];

    /// <summary>
    /// Connects this source to a localization manager and starts listening for locale changes.
    /// </summary>
    /// <param name="manager">The localization manager to bridge.</param>
    public void Attach(ILocalizationManager manager)
    {
        _manager = manager;
        manager.LocaleChanged += OnLocaleChanged;
        RaiseIndexerChanged();
    }

    private void OnLocaleChanged(object? sender, EventArgs e) => RaiseIndexerChanged();

    private void RaiseIndexerChanged()
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
}
