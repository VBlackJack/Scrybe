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

using System.Windows;

namespace Scrybe.App.Views;

/// <summary>Dracula-themed modal confirmation dialog for destructive actions.</summary>
public sealed partial class ConfirmationDialog : Window
{
    /// <summary>Initializes the dialog with localized content and button labels.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Dialog message.</param>
    /// <param name="okText">Confirm button label.</param>
    /// <param name="cancelText">Cancel button label.</param>
    public ConfirmationDialog(string title, string message, string okText, string cancelText)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(okText);
        ArgumentNullException.ThrowIfNull(cancelText);

        DialogTitle = title;
        DialogMessage = message;
        OkText = okText;
        CancelText = cancelText;

        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>Dialog title shown in the window chrome and content.</summary>
    public string DialogTitle { get; }

    /// <summary>Dialog message shown in the content area.</summary>
    public string DialogMessage { get; }

    /// <summary>Confirm button label.</summary>
    public string OkText { get; }

    /// <summary>Cancel button label.</summary>
    public string CancelText { get; }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        CancelButton.Focus();
    }

    private void OnOkClicked(object sender, RoutedEventArgs args)
    {
        DialogResult = true;
    }
}
