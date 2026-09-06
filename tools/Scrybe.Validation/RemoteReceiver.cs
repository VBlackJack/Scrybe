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
using Scrybe.Core.Interfaces;
using Scrybe.Core.Text;

namespace Scrybe.Validation;

/// <summary>Passive receiver for an external Scrybe sender, including RDP/Citrix sessions.</summary>
internal static class RemoteReceiver
{
    private const int Width = 640;
    private const int Height = 420;
    internal static Task RunAsync(string output, ILocalizationManager localization)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TextBox actual = new() { AcceptsReturn = true, AcceptsTab = true, MinHeight = Height / 3d };
        AutomationProperties.SetName(actual, localization["Validation.Receiver"]);
        TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        Button compare = new() { Content = localization["Validation.Compare"] };
        AutomationProperties.SetName(compare, localization["Validation.Compare"]);
        TextBox expected = new() { Text = NativeValidation.ReferencePayload, IsReadOnly = true, AcceptsReturn = true, AcceptsTab = true };
        AutomationProperties.SetName(expected, localization["Validation.Expected"]);
        StackPanel panel = new();
        panel.Children.Add(new TextBlock { Text = localization["Validation.ReceiverHelp"], TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(expected);
        panel.Children.Add(actual);
        panel.Children.Add(compare);
        panel.Children.Add(status);
        Window window = new() { Title = localization["Validation.NativeTitle"], Width = Width, Height = Height, Content = panel };
        int attempt = 0;
        compare.Click += async (_, _) =>
        {
            compare.IsEnabled = false;
            try
            {
                string received = actual.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
                double cer = TextAccuracy.CharacterErrorRate(NativeValidation.ReferencePayload, received);
                bool passed = received == NativeValidation.ReferencePayload;
                string file = Path.Combine(output, $"receiver-{++attempt:D3}.json");
                // Raw received text and key logs are deliberately excluded from passive receiver reports.
                await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new
                {
                    passed,
                    cer,
                    receivedCharacters = received.Length,
                    expectedCharacters = NativeValidation.ReferencePayload.Length,
                    caseId = "basic-tab-enter",
                    capturedAtUtc = DateTimeOffset.UtcNow
                }, Program.Json));
                status.Text = localization[passed ? "Validation.Match" : "Validation.Mismatch"];
            }
            catch (Exception exception)
            {
                Scrybe.Core.Logging.FileLogger.Error("Could not write receiver result.", exception);
                status.Text = localization["Persist.SaveFailed"];
            }
            finally { compare.IsEnabled = true; }
        };
        window.Closed += (_, _) => completion.TrySetResult();
        window.Show();
        actual.Focus();
        return completion.Task;
    }
}
