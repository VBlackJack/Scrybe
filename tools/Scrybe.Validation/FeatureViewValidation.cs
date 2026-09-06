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
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Scrybe.App.ViewModels;
using Scrybe.App.Views;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Scrybe.Core.Snippets;

namespace Scrybe.Validation;

/// <summary>Renders new modal views and verifies bound data and accessible action geometry.</summary>
internal static class FeatureViewValidation
{
    private const int Width = 920;
    private const int Height = 620;
    internal static async Task RunAsync(string output, ILocalizationManager localization)
    {
        List<object> results = [];
        foreach (string locale in new[] { "en", "fr" })
        {
            await localization.LoadAsync(locale);
            foreach (double dpi in new[] { 96d, 192d })
            {
                DrawingVisual source = new();
                using (DrawingContext drawing = source.RenderOpen())
                {
                    drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, 240, 100));
                    drawing.DrawText(new FormattedText("  synthetic OCR\n  source crop",
                        System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Consolas"), 16, Brushes.Black, 1), new Point(0, 10));
                }
                RenderTargetBitmap crop = new(240, 100, 96, 96, PixelFormats.Pbgra32);
                crop.Render(source);
                crop.Freeze();
                OcrReviewViewModel draft = new("  synthetic OCR\n\teditable");
                Snippet snippet = new("one", "Synthetic template", "Validation", "  echo {{value}}\n", [new("value", "Value", "synthetic")]);
                DataReviewViewModel import = new(localization["Exchange.Import"],
                    string.Format(System.Globalization.CultureInfo.CurrentCulture, localization["Exchange.Preview"], 1, 1),
                    [new(snippet.Name, snippet)],
                    [new(localization["Exchange.KeepExisting"], SnippetConflictPolicy.KeepExisting)]);
                Window[] windows = [new OcrReviewWindow(crop, draft), new DataReviewWindow(import)];
                foreach (Window window in windows)
                {
                    try
                    {
                        FrameworkElement content = (FrameworkElement)window.Content;
                        content.Measure(new Size(Width, Height)); content.Arrange(new Rect(0, 0, Width, Height)); content.UpdateLayout();
                        Button[] buttons = Descendants(content).OfType<Button>().Where(button => button.Command is not null).ToArray();
                        bool actions = buttons.Length == 2 && buttons.All(button =>
                        {
                            Point point = button.TransformToAncestor(content).Transform(new Point());
                            return !string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))
                                && point.X >= 0 && point.Y >= 0 && point.X + button.ActualWidth <= Width
                                && point.Y + button.ActualHeight <= Height;
                        });
                        bool textBound = Descendants(content).OfType<TextBox>().Any(box => box.Text.Contains("synthetic", StringComparison.Ordinal));
                        bool policyVisible = window is not DataReviewWindow || Descendants(content).OfType<TextBlock>()
                            .Any(block => block.Text == localization["Exchange.KeepExisting"]);
                        if (!policyVisible) { throw new InvalidOperationException("The selected conflict policy is not visible."); }
                        if (!actions || !textBound) { throw new InvalidOperationException("Feature view binding or action bounds failed."); }
                        RenderTargetBitmap render = new((int)(Width * dpi / 96), (int)(Height * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
                        render.Render(content);
                        PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(render));
                        using FileStream file = File.Create(Path.Combine(output, $"{window.GetType().Name}-{locale}-{dpi}.png"));
                        encoder.Save(file);
                        results.Add(new { locale, dpi, view = window.GetType().Name, actions, textBound });
                    }
                    finally { window.Close(); }
                }
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "features.json"), JsonSerializer.Serialize(results, Program.Json));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) { yield return descendant; }
        }
    }
}
