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
using Scrybe.Core.Localization;
using Scrybe.Core.Logging;

namespace Scrybe.Validation;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 2 || args[0] is not ("ocr" or "overlay" or "native" or "receiver")) { return 2; }
        string mode = args[0];
        string output = Path.GetFullPath(args[1]);
        // All evidence is created in a new folder. Never overwrite an earlier measurement.
        if (Directory.Exists(output)) { return 2; }
        Directory.CreateDirectory(output);
        FileLogger.Initialize(Path.Combine(output, "logs"));
        Application app = new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Scrybe;component/Themes/DarkTheme.xaml") });
        app.Dispatcher.BeginInvoke(async () =>
        {
            int exitCode = 0;
            try
            {
                LocalizationManager localization = new(Path.Combine(AppContext.BaseDirectory, "locales"));
                await localization.LoadAsync("en");
                Scrybe.App.Localization.LocalizationSource.Instance.Attach(localization);
                switch (mode)
                {
                    case "ocr":
                        await OcrBenchmark.RunAsync(output, args.Length > 2 ? args[2] : Path.Combine(AppContext.BaseDirectory, "corpus.json"));
                        break;
                    case "overlay":
                        await OverlayValidation.RunAsync(output, localization);
                        break;
                    case "receiver":
                        await RemoteReceiver.RunAsync(output, localization);
                        break;
                    case "native":
                        if (!args.Contains("--allow-input", StringComparer.Ordinal)) { throw new InvalidOperationException("Native input requires --allow-input on a controlled desktop."); }
                        string layout = args.FirstOrDefault(value => value.StartsWith("--layout=", StringComparison.Ordinal))?.Split('=')[1] ?? "current";
                        string scenario = args.FirstOrDefault(value => value.StartsWith("--scenario=", StringComparison.Ordinal))?.Split('=')[1] ?? "complete";
                        await NativeValidation.RunAsync(output, localization, layout, scenario);
                        break;
                }
            }
            catch (Exception exception)
            {
                FileLogger.Error($"Validation mode {mode} failed.", exception);
                await File.WriteAllTextAsync(Path.Combine(output, "failure.json"), JsonSerializer.Serialize(new { mode, error = exception.ToString() }, Json));
                exitCode = 1;
            }
            finally { FileLogger.Flush(); app.Shutdown(exitCode); }
        });
        return app.Run();
    }
}
