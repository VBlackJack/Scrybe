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
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Scrybe.App.Views;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;

namespace Scrybe.Validation;

internal static class OverlayValidation
{
    private static double Contrast(Color foreground, Color background)
    {
        static double Linear(byte component)
        {
            double value = component / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        double first = Luminance(foreground);
        double second = Luminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private const int Width = 1280;
    private const int Height = 720;
    internal static async Task RunAsync(string output, ILocalizationManager localization)
    {
        List<object> results = [];
        foreach (string locale in new[] { "en", "fr" })
        {
            await localization.LoadAsync(locale);
            foreach (double dpi in new[] { 96d, 144d, 192d })
            {
                byte[] pixels = new byte[Width * Height * 4];
                BitmapSource bitmap = BitmapSource.Create(Width, Height, dpi, dpi, PixelFormats.Bgra32, null, pixels, Width * 4);
                bitmap.Freeze();
                CapturedFrame frame = new(pixels, Width, Height, dpi, dpi, -Width, 0);
                CaptureOverlayWindow overlay = new(bitmap, frame, localization);
                try
                {
                    FrameworkElement content = (FrameworkElement)overlay.Content;
                    double scale = dpi / 96d;
                    content.Measure(new Size(Width / scale, Height / scale));
                    content.Arrange(new Rect(0, 0, Width / scale, Height / scale));
                    content.UpdateLayout();
                    overlay.HandleSelectionKey(Key.K, ModifierKeys.None);
                    overlay.HandleSelectionKey(Key.Right, ModifierKeys.Control);
                    overlay.HandleSelectionKey(Key.Down, ModifierKeys.Shift);
                    content.UpdateLayout();
                    AutomationPeer? peer = UIElementAutomationPeer.CreatePeerForElement(overlay);
                    TextBlock readout = (TextBlock)overlay.FindName("ReadoutText");
                    bool named = peer?.GetName() == localization["Overlay.Title"];
                    Border hint = (Border)overlay.FindName("HintBorder");
                    Border readoutBorder = (Border)overlay.FindName("ReadoutBorder");
                    bool bounded = hint.ActualWidth <= Width / scale && readoutBorder.ActualWidth <= Width / scale;
                    TextBlock hintText = (TextBlock)overlay.FindName("HintText");
                    double hintContrast = Contrast(((SolidColorBrush)hintText.Foreground).Color, ((SolidColorBrush)hint.Background).Color);
                    double readoutContrast = Contrast(((SolidColorBrush)readout.Foreground).Color, ((SolidColorBrush)readoutBorder.Background).Color);
                    bool announced = AutomationProperties.GetName(readout).Contains("490", StringComparison.Ordinal);
                    bool polite = AutomationProperties.GetLiveSetting(readout) == AutomationLiveSetting.Polite;
                    RenderTargetBitmap render = new(Width, Height, dpi, dpi, PixelFormats.Pbgra32);
                    render.Render(content);
                    PngBitmapEncoder encoder = new();
                    encoder.Frames.Add(BitmapFrame.Create(render));
                    using FileStream file = File.Create(Path.Combine(output, $"overlay-{locale}-{dpi}.png"));
                    encoder.Save(file);
                    results.Add(new { locale, dpi, monitorLeft = frame.MonitorLeft, named, polite, bounded, announced, hintContrast, readoutContrast });
                    if (!named || !polite || !bounded || !announced || hintContrast < 4.5 || readoutContrast < 4.5) { throw new InvalidOperationException("Overlay accessibility contract failed."); }
                }
                finally { overlay.Close(); }
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "overlay.json"), JsonSerializer.Serialize(results, Program.Json));
        await RenderRecoveryViewsAsync(output, localization);
        await FeatureViewValidation.RunAsync(output, localization);
    }

    private static async Task RenderRecoveryViewsAsync(string output, ILocalizationManager localization)
    {
        const int ViewWidth = 720;
        const int ViewHeight = 600;
        List<object> results = [];
        foreach (string locale in new[] { "en", "fr" })
        {
            await localization.LoadAsync(locale);
            UserControl[] views = [new SecretManagerView(), new SnippetManagerView(), new HistoryView(), new SettingsView()];
            foreach (UserControl view in views)
            {
                view.Measure(new Size(ViewWidth, ViewHeight));
                view.Arrange(new Rect(0, 0, ViewWidth, ViewHeight));
                view.UpdateLayout();
                Button recovery = Descendants(view).OfType<Button>().Single(button =>
                    AutomationProperties.GetName(button) == localization["Persist.Reload"]);
                Point location = recovery.TransformToAncestor(view).Transform(new Point());
                bool bounded = recovery.ActualWidth > 0 && location.X >= 0 && location.X + recovery.ActualWidth <= ViewWidth;
                RenderTargetBitmap render = new(ViewWidth, ViewHeight, 96, 96, PixelFormats.Pbgra32);
                render.Render(view);
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(render));
                using FileStream file = File.Create(Path.Combine(output, $"{view.GetType().Name}-{locale}.png"));
                encoder.Save(file);
                results.Add(new { locale, view = view.GetType().Name, bounded, location.X, location.Y });
                if (!bounded) { throw new InvalidOperationException("Recovery action extends outside the manager view."); }
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "managers.json"), JsonSerializer.Serialize(results, Program.Json));
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
