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

using System.Windows.Data;
using System.Windows.Markup;
using Binding = System.Windows.Data.Binding;

namespace Scrybe.App.Localization;

/// <summary>
/// XAML markup extension that resolves a localized string by key and keeps the bound
/// target in sync when the active locale changes. Usage: <c>{loc:Translate AppTitle}</c>.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TranslateExtension : MarkupExtension
{
    /// <summary>Initializes a new instance with an empty key.</summary>
    public TranslateExtension()
    {
        Key = string.Empty;
    }

    /// <summary>Initializes a new instance for the given translation key.</summary>
    /// <param name="key">The translation key to resolve.</param>
    public TranslateExtension(string key)
    {
        Key = key;
    }

    /// <summary>The translation key to resolve against the active locale.</summary>
    [ConstructorArgument("key")]
    public string Key { get; set; }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        Binding binding = new($"[{Key}]")
        {
            Source = LocalizationSource.Instance,
            Mode = BindingMode.OneWay,
        };

        return binding.ProvideValue(serviceProvider);
    }
}
