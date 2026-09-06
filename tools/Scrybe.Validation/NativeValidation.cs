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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Scrybe.App.Services;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Scrybe.Core.Text;

namespace Scrybe.Validation;

internal static class NativeValidation
{
    internal const string ReferencePayload = "ABC\tdef\nQWERTY azerty 123!?";
    private const int WindowWidth = 640;
    private const int WindowHeight = 300;
    private const int SettleMs = 200;
    private const int KeyDelayMs = 20;
    private const int InterruptAt = 5;
    private const int TimeoutSeconds = 30;
    private const uint ActivateForThread = 0;

    internal static async Task RunAsync(string output, ILocalizationManager localization, string requestedLayout, string scenario)
    {
        if (scenario is not ("complete" or "focus" or "cancel")) { throw new ArgumentException("Unknown native scenario."); }
        IntPtr originalLayout = GetKeyboardLayout(0);
        IntPtr layout = originalLayout;
        if (requestedLayout != "current")
        {
            if (!uint.TryParse(requestedLayout, System.Globalization.NumberStyles.HexNumber, null, out uint language)) { throw new ArgumentException("Invalid layout ID."); }
            IntPtr[] installed = new IntPtr[GetKeyboardLayoutList(0, null)];
            GetKeyboardLayoutList(installed.Length, installed);
            layout = installed.FirstOrDefault(item => (item.ToInt64() & 0xffff) == (language & 0xffff));
            if (layout == IntPtr.Zero) { throw new InvalidOperationException("Requested layout is not installed; no layout was installed by this tool."); }
        }
        TextBox receiver = new() { AcceptsReturn = true, AcceptsTab = true };
        System.Windows.Automation.AutomationProperties.SetName(receiver, localization["Validation.Receiver"]);
        Window window = new() { Title = localization["Validation.NativeTitle"], Width = WindowWidth, Height = WindowHeight, Content = receiver };
        Window distraction = new() { Title = localization["Validation.FocusTarget"], Width = WindowWidth, Height = WindowHeight };
        List<object> observations = [];
        List<object> results = [];
        bool allPassed = true;
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(TimeoutSeconds));
        window.Closed += (_, _) => deadline.Cancel();
        try
        {
            ActivateKeyboardLayout(layout, ActivateForThread);
            window.Show();
            IntPtr handle = new WindowInteropHelper(window).Handle;
            InteropTargetWindowGateway gateway = new();
            foreach (bool scancode in new[] { false, true })
            {
                using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                receiver.Text = string.Empty;
                observations.Clear();
                bool interrupted = false;
                TextChangedEventHandler changed = (_, _) =>
                {
                    if (interrupted || receiver.Text.Length < InterruptAt) { return; }
                    interrupted = true;
                    if (scenario == "focus") { distraction.Show(); distraction.Activate(); }
                    if (scenario == "cancel") { cancellation.Cancel(); }
                };
                KeyEventHandler key = (_, e) => observations.Add(new { key = e.Key.ToString(), modifiers = Keyboard.Modifiers.ToString() });
                receiver.TextChanged += changed;
                receiver.PreviewKeyDown += key;
                InjectionResult result;
                try
                {
                    window.Activate();
                    receiver.Focus();
                    await Task.Delay(SettleMs, deadline.Token);
                    if (gateway.GetForegroundWindow() != handle || !gateway.TryGetInfo(handle, out TargetWindowInfo? info) || info is null)
                    { throw new InvalidOperationException("Controlled receiver does not own foreground; refusing input."); }
                    ConfirmedInjectionContext context = new(gateway, info);
                    AppSettings settings = new() { InjectionKeyDelayMs = KeyDelayMs };
                    IKeystrokeInjector injector = scancode ? new ScancodeInjector(settings) : new UnicodeInjector(settings);
                    result = await injector.InjectAsync(ReferencePayload.AsMemory(), cancellation.Token, context);
                    await Task.Delay(SettleMs, deadline.Token);
                }
                finally
                {
                    receiver.TextChanged -= changed;
                    receiver.PreviewKeyDown -= key;
                }
                string actual = receiver.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
                bool passed = scenario == "complete" ? result.Success && actual == ReferencePayload
                    : result.Aborted && actual.Length < ReferencePayload.Length;
                allPassed &= passed;
                results.Add(new
                {
                    scancode,
                    scenario,
                    layout = layout.ToInt64().ToString("X"),
                    result,
                    passed,
                    expected = ReferencePayload,
                    actual,
                    cer = TextAccuracy.CharacterErrorRate(ReferencePayload, actual),
                    keys = observations.ToArray()
                });
            }
            await File.WriteAllTextAsync(Path.Combine(output, "native.json"), JsonSerializer.Serialize(results, Program.Json));
            if (!allPassed) { throw new InvalidOperationException("Native receiver assertions failed; see native.json."); }
        }
        finally
        {
            ActivateKeyboardLayout(originalLayout, ActivateForThread);
            window.Close();
            distraction.Close();
        }
    }

    [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll")] private static extern int GetKeyboardLayoutList(int count, [Out] IntPtr[]? layouts);
    [DllImport("user32.dll")] private static extern IntPtr ActivateKeyboardLayout(IntPtr layout, uint flags);
}
