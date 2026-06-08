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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrybe.Core.Models;

namespace Scrybe.App.ViewModels;

/// <summary>View model for selecting a stored secret and requesting injection.</summary>
public sealed partial class SecretPaletteViewModel : ObservableObject
{
    [ObservableProperty]
    private SecretEntry? _selectedSecret;

    [ObservableProperty]
    private bool _canInject;

    /// <summary>Initializes the palette with the available secrets.</summary>
    /// <param name="secrets">The protected secret library entries.</param>
    public SecretPaletteViewModel(IReadOnlyList<SecretEntry> secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        Secrets = new ObservableCollection<SecretEntry>(secrets);
        SelectedSecret = Secrets.FirstOrDefault();
    }

    /// <summary>Raised with the selected secret id when the user confirms injection.</summary>
    public event EventHandler<string>? InjectRequested;

    /// <summary>The selectable secret entries.</summary>
    public ObservableCollection<SecretEntry> Secrets { get; }

    partial void OnSelectedSecretChanged(SecretEntry? value) => CanInject = value is not null;

    [RelayCommand]
    private void Inject()
    {
        if (SelectedSecret is not null)
        {
            InjectRequested?.Invoke(this, SelectedSecret.Id);
        }
    }
}
