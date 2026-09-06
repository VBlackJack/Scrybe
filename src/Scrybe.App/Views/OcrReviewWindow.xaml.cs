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
using System.Windows.Media.Imaging;
using Scrybe.App.ViewModels;

namespace Scrybe.App.Views;

/// <summary>Shows the original crop alongside a transient, editable OCR draft.</summary>
public sealed partial class OcrReviewWindow : Window
{
    /// <summary>Creates a review without copying, saving or logging its contents.</summary>
    public OcrReviewWindow(BitmapSource crop, OcrReviewViewModel viewModel)
    {
        InitializeComponent();
        Crop.Source = crop;
        DataContext = viewModel;
        viewModel.Completed += OnCompleted;
        Closed += (_, _) => { viewModel.Completed -= OnCompleted; Crop.Source = null; DataContext = null; };
        Loaded += (_, _) => Editor.Focus();
        Editor.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Tab && System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            {
                Editor.MoveFocus(new System.Windows.Input.TraversalRequest(
                    System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)
                        ? System.Windows.Input.FocusNavigationDirection.Previous : System.Windows.Input.FocusNavigationDirection.Next));
                args.Handled = true;
            }
        };
    }

    private void OnCompleted(object? sender, bool accepted) => DialogResult = accepted;
}
