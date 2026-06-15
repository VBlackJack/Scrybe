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

using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Scrybe.App.Localization;
using Scrybe.App.Services;
using Scrybe.App.Theming;
using Scrybe.App.ViewModels;
using Scrybe.App.Views;
using Scrybe.Core;
using Scrybe.Core.History;
using Scrybe.Core.Input;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Localization;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;
using Scrybe.Core.Secrets;
using Scrybe.Core.Security;
using Scrybe.Core.Services;
using Scrybe.Core.Settings;
using Scrybe.Core.Snippets;
using Scrybe.Ocr;

namespace Scrybe.App;

/// <summary>
/// Application entry point. Wires up logging, global exception handling, dependency injection,
/// localization, theming, the tray icon, the global capture hotkey, the OCR-to-clipboard flow, and
/// the optional startup pre-warm.
/// </summary>
public partial class App : System.Windows.Application
{
    private const string ThemeDictionaryUri = "pack://application:,,,/Themes/DarkTheme.xaml";

    private ServiceProvider? _serviceProvider;
    private TrayIconService? _trayIconService;
    private IHotkeyService? _hotkeyService;
    private CaptureCoordinator? _captureCoordinator;
    private InjectionCoordinator? _injectionCoordinator;
    private ClipboardInjectionCoordinator? _clipboardInjectionCoordinator;
    private SnippetCoordinator? _snippetCoordinator;
    private SecretCoordinator? _secretCoordinator;
    private CaptureHistoryCoordinator? _historyCoordinator;
    private CaptureHistoryLibrary? _historyLibrary;
    private MainViewModel? _mainViewModel;
    private bool _isShutdownRequested;

    /// <summary>Whether a real application shutdown has been requested, rather than a hub hide.</summary>
    internal bool IsShutdownRequested => _isShutdownRequested;

    /// <summary>Requests a real application shutdown from tray-only exit commands.</summary>
    internal void RequestShutdown()
    {
        _isShutdownRequested = true;
        Shutdown();
    }

    /// <inheritdoc />
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppConstants.AppName,
            AppConstants.LogSubDirName);
        FileLogger.Initialize(logDirectory);
        FileLogger.Info($"{AppConstants.AppName} starting.");

        RegisterExceptionHandlers();

        ISettingsStore settingsStore = BuildSettingsStore();
        SettingsLoadResult loaded = await settingsStore.LoadAsync().ConfigureAwait(true);
        AppSettings settings = loaded.Settings;
        if (!loaded.Existed)
        {
            FileLogger.Info("No settings file found; writing defaults.");
            bool persistedDefaults = await settingsStore.SaveAsync(settings).ConfigureAwait(true);
            if (!persistedDefaults)
            {
                FileLogger.Warn("Default settings were created in memory but could not be persisted.");
            }
        }

        FileLogger.SetEnabled(settings.EnableLogging);

        ServiceCollection services = new();
        ConfigureServices(services, settings);
        _serviceProvider = services.BuildServiceProvider();

        ILocalizationManager localization = _serviceProvider.GetRequiredService<ILocalizationManager>();
        await localization.LoadAsync(settings.LocaleCode).ConfigureAwait(true);
        LocalizationSource.Instance.Attach(localization);

        ApplyTheme(settings.DefaultThemeId);

        MainWindow mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        InitializeServices(settings);

        await _serviceProvider.GetRequiredService<SnippetLibrary>().LoadAsync().ConfigureAwait(true);
        await _serviceProvider.GetRequiredService<SecretLibrary>().LoadAsync().ConfigureAwait(true);
        await _serviceProvider.GetRequiredService<CaptureHistoryLibrary>().LoadAsync().ConfigureAwait(true);
        _serviceProvider.GetRequiredService<SnippetManagerViewModel>().Reload();
        _serviceProvider.GetRequiredService<SecretManagerViewModel>().Reload();
        _serviceProvider.GetRequiredService<HistoryManagerViewModel>().Reload();
        _mainViewModel?.RefreshStatus();

        FileLogger.Info("Startup complete.");
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        FileLogger.Info($"{AppConstants.AppName} shutting down.");

        if (_trayIconService is not null)
        {
            _trayIconService.CaptureRequested -= OnCaptureRequested;
            _trayIconService.ShowHubRequested -= OnShowHubRequested;
            _trayIconService.ManageSnippetsRequested -= OnManageSnippetsRequested;
            _trayIconService.ManageSecretsRequested -= OnManageSecretsRequested;
            _trayIconService.ShowHistoryRequested -= OnShowHistoryRequested;
            _trayIconService.CleanupModeChanged -= OnCleanupModeChanged;
            _trayIconService.SettingsRequested -= OnSettingsRequested;
            _trayIconService.ShowAboutRequested -= OnAboutRequested;
        }

        if (_mainViewModel is not null)
        {
            _mainViewModel.CaptureRequested -= OnCaptureRequested;
            _mainViewModel.CleanupModeChanged -= OnCleanupModeChanged;
            _mainViewModel.Settings.Saved -= OnSettingsSaved;
            _mainViewModel.Settings.InjectionReferenceRequested -= OnInjectionReferenceRequested;
        }

        if (_historyLibrary is not null)
        {
            _historyLibrary.EntriesChanged -= OnHistoryEntriesChanged;
        }

        if (_hotkeyService is not null)
        {
            _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
            _hotkeyService.Dispose();
        }

        _trayIconService?.Dispose();

        FileLogger.Flush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Registers the dependency-injection graph for the application.</summary>
    private static void ConfigureServices(IServiceCollection services, AppSettings settings)
    {
        string localesDirectory = Path.Combine(AppContext.BaseDirectory, AppConstants.LocalesDirName);
        string tessdataDirectory = Path.Combine(AppContext.BaseDirectory, AppConstants.TessdataDirName);

        services.AddSingleton(settings);
        services.AddSingleton<ISettingsStore>(_ => BuildSettingsStore());
        services.AddSingleton<ILocalizationManager>(_ => new LocalizationManager(localesDirectory));
        services.AddSingleton<AboutInfoProvider>();
        services.AddSingleton<DiagnosticsInfoProvider>();
        services.AddSingleton<ISystemShell, SystemShell>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SnippetManagerViewModel>();
        services.AddSingleton<SecretManagerViewModel>();
        services.AddSingleton<HistoryManagerViewModel>();

        services.AddSingleton<IScreenCaptureService, WgcScreenCaptureService>();
        services.AddSingleton<IOcrEngine>(_ => new TesseractOcrEngine(tessdataDirectory, settings.OcrLanguage));
        services.AddSingleton<IClipboardWriter, WpfClipboardWriter>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IOcrTextStore, OcrTextStore>();
        services.AddSingleton<UnicodeInjector>();
        services.AddSingleton<ScancodeInjector>();

        services.AddSingleton<TrayIconService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<TrayIconService>());
        services.AddSingleton<IConfirmationService, ThemedConfirmationService>();
        services.AddSingleton<IStartupRegistration, StartupRegistrationService>();

        string snippetsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppConstants.AppName,
            AppConstants.SnippetsFileName);
        services.AddSingleton<ISnippetStore>(_ => new JsonSnippetStore(snippetsPath));
        services.AddSingleton<SnippetLibrary>();
        services.AddSingleton<SnippetCoordinator>();

        string secretsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppConstants.AppName,
            AppConstants.SecretsFileName);
        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddSingleton<ISecretStore>(_ => new JsonSecretStore(secretsPath));
        services.AddSingleton<SecretLibrary>();
        services.AddSingleton<SecretCoordinator>();

        string historyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppConstants.AppName,
            AppConstants.CaptureHistoryFileName);
        services.AddSingleton<ICaptureHistoryStore>(_ => new JsonCaptureHistoryStore(historyPath));
        services.AddSingleton<CaptureHistoryLibrary>();
        services.AddSingleton<CaptureHistoryCoordinator>();

        services.AddSingleton<CaptureCoordinator>();
        services.AddSingleton<ITargetWindowGateway, InteropTargetWindowGateway>();
        services.AddSingleton<ITargetConfirmationPrompt, NativeTargetConfirmationPrompt>();
        services.AddSingleton<InjectionCoordinator>(sp => new InjectionCoordinator(
            sp.GetRequiredService<UnicodeInjector>(),
            sp.GetRequiredService<ScancodeInjector>(),
            sp.GetRequiredService<IOcrTextStore>(),
            sp.GetRequiredService<INotificationService>(),
            sp.GetRequiredService<ILocalizationManager>(),
            sp.GetRequiredService<AppSettings>()));
        services.AddSingleton<InjectionTargetConfirmer>();
        services.AddSingleton<IInjectionTargetConfirmer>(sp => sp.GetRequiredService<InjectionTargetConfirmer>());
        services.AddSingleton<ClipboardInjectionCoordinator>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<HotkeyRegistrar>();
    }

    /// <summary>Shows the tray icon, registers the hotkey, and starts the optional pre-warm.</summary>
    private void InitializeServices(AppSettings settings)
    {
        IServiceProvider provider = _serviceProvider!;

        _captureCoordinator = provider.GetRequiredService<CaptureCoordinator>();
        _injectionCoordinator = provider.GetRequiredService<InjectionCoordinator>();
        _clipboardInjectionCoordinator = provider.GetRequiredService<ClipboardInjectionCoordinator>();
        _snippetCoordinator = provider.GetRequiredService<SnippetCoordinator>();
        _secretCoordinator = provider.GetRequiredService<SecretCoordinator>();
        _historyCoordinator = provider.GetRequiredService<CaptureHistoryCoordinator>();
        _historyLibrary = provider.GetRequiredService<CaptureHistoryLibrary>();
        _historyLibrary.EntriesChanged += OnHistoryEntriesChanged;

        _mainViewModel = provider.GetRequiredService<MainViewModel>();
        _mainViewModel.CaptureRequested += OnCaptureRequested;
        _mainViewModel.CleanupModeChanged += OnCleanupModeChanged;
        _mainViewModel.Settings.Saved += OnSettingsSaved;
        _mainViewModel.Settings.InjectionReferenceRequested += OnInjectionReferenceRequested;

        _trayIconService = provider.GetRequiredService<TrayIconService>();
        _trayIconService.CaptureRequested += OnCaptureRequested;
        _trayIconService.ShowHubRequested += OnShowHubRequested;
        _trayIconService.ManageSnippetsRequested += OnManageSnippetsRequested;
        _trayIconService.ManageSecretsRequested += OnManageSecretsRequested;
        _trayIconService.ShowHistoryRequested += OnShowHistoryRequested;
        _trayIconService.CleanupModeChanged += OnCleanupModeChanged;
        _trayIconService.SettingsRequested += OnSettingsRequested;
        _trayIconService.ShowAboutRequested += OnAboutRequested;
        _trayIconService.Initialize();

        _hotkeyService = provider.GetRequiredService<IHotkeyService>();
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        List<HotkeyBinding> userHotkeys =
        [
            new HotkeyBinding(AppConstants.CaptureHotkeyId, settings.HotkeyModifiers, settings.HotkeyKey),
            new HotkeyBinding(AppConstants.InjectHotkeyId, settings.InjectHotkeyModifiers, settings.InjectHotkeyKey),
            new HotkeyBinding(
                AppConstants.ClipboardInjectHotkeyId,
                settings.ClipboardInjectHotkeyModifiers,
                settings.ClipboardInjectHotkeyKey),
            new HotkeyBinding(AppConstants.AbortHotkeyId, settings.AbortHotkeyModifiers, settings.AbortHotkeyKey),
            new HotkeyBinding(AppConstants.PaletteHotkeyId, settings.PaletteHotkeyModifiers, settings.PaletteHotkeyKey),
            new HotkeyBinding(AppConstants.SecretPaletteHotkeyId, settings.SecretPaletteHotkeyModifiers, settings.SecretPaletteHotkeyKey),
            new HotkeyBinding(
                AppConstants.CaptureHistoryHotkeyId,
                settings.HistoryPaletteHotkeyModifiers,
                settings.HistoryPaletteHotkeyKey),
        ];
        provider.GetRequiredService<HotkeyRegistrar>().Initialize(userHotkeys);

        if (settings.DebugInjectionEnabled)
        {
            RegisterHotkey(AppConstants.InjectReference100HotkeyId, AppConstants.DefaultDebugHotkeyModifiers, AppConstants.DefaultInjectReference100Key);
            RegisterHotkey(AppConstants.InjectReference500HotkeyId, AppConstants.DefaultDebugHotkeyModifiers, AppConstants.DefaultInjectReference500Key);
            RegisterHotkey(AppConstants.InjectReference1000HotkeyId, AppConstants.DefaultDebugHotkeyModifiers, AppConstants.DefaultInjectReference1000Key);
            RegisterHotkey(AppConstants.InjectReference1000AltHotkeyId, AppConstants.DefaultDebugHotkeyModifiers, AppConstants.DefaultInjectReference1000AltKey);
            RegisterHotkey(AppConstants.ToggleInjectionModeHotkeyId, AppConstants.DefaultDebugHotkeyModifiers, AppConstants.DefaultToggleInjectionModeKey);
            RegisterHotkey(AppConstants.CycleInjectionPacingHotkeyId, AppConstants.DefaultDebugHotkeyModifiers, AppConstants.DefaultCycleInjectionPacingKey);
        }

        // Conservative self-heal: re-register only when our own entry points at a now-missing
        // executable. Never throws, so it cannot block startup.
        provider.GetRequiredService<IStartupRegistration>().HealIfStale();

        if (settings.PrewarmOnStartup)
        {
            StartPrewarm(provider);
        }
    }

    private void RegisterHotkey(string id, string modifiers, string key)
    {
        if (HotkeyParser.TryParse(modifiers, key, out HotkeyDefinition? definition) && definition is not null)
        {
            _hotkeyService!.TryRegister(id, definition);
        }
        else
        {
            FileLogger.Warn($"Invalid hotkey configuration for '{id}': '{modifiers}' + '{key}'.");
        }
    }

    /// <summary>Pre-initializes the capture device and OCR engine off the UI thread, without blocking startup.</summary>
    private static void StartPrewarm(IServiceProvider provider)
    {
        IScreenCaptureService capture = provider.GetRequiredService<IScreenCaptureService>();
        IOcrEngine ocr = provider.GetRequiredService<IOcrEngine>();

        _ = Task.Run(() =>
        {
            long startTimestamp = Stopwatch.GetTimestamp();
            FileLogger.Info("Pre-warm started (capture device + OCR engine).");

            try
            {
                capture.Prewarm();
            }
            catch (Exception exception)
            {
                FileLogger.Error("Capture device pre-warm failed; falling back to lazy init.", exception);
            }

            try
            {
                ocr.Prewarm();
            }
            catch (Exception exception)
            {
                FileLogger.Error("OCR engine pre-warm failed; falling back to lazy init.", exception);
            }

            FileLogger.Info($"Pre-warm complete in {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds:F0}ms.");
        });
    }

    private void OnCaptureRequested(object? sender, EventArgs e) => _ = CaptureFromRequestAsync(sender);

    private async Task CaptureFromRequestAsync(object? sender)
    {
        if (ReferenceEquals(sender, _mainViewModel))
        {
            HideHubWindow();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        }

        await _captureCoordinator!.CaptureAsync().ConfigureAwait(true);
    }

    private void OnInjectionReferenceRequested(object? sender, int length) => _ = InjectReferenceFromRequestAsync(length);

    private async Task InjectReferenceFromRequestAsync(int length)
    {
        HideHubWindow();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await Task.Delay(AppConstants.InjectionStartDelayMs).ConfigureAwait(true);
        await _injectionCoordinator!.InjectReferenceAsync(length).ConfigureAwait(true);
    }

    private void HideHubWindow()
    {
        if (MainWindow is Window window && window.IsVisible)
        {
            window.Hide();
        }
    }

    private void OnShowHubRequested(object? sender, EventArgs e)
    {
        ShowShellWindow();
    }

    private void ShowShellWindow()
    {
        if (MainWindow is not Window window)
        {
            return;
        }

        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    private void OnManageSnippetsRequested(object? sender, EventArgs e)
    {
        ShowShellWindow();
        _mainViewModel?.ShowSnippetsTab();
    }

    private void OnManageSecretsRequested(object? sender, EventArgs e)
    {
        ShowShellWindow();
        _mainViewModel?.ShowSecretsTab();
    }

    private void OnShowHistoryRequested(object? sender, EventArgs e)
    {
        ShowShellWindow();
        _mainViewModel?.ShowHistoryTab();
    }

    private void OnHistoryEntriesChanged(object? sender, EventArgs e)
        => Dispatcher.InvokeAsync(() =>
        {
            _mainViewModel?.RefreshStatus();
            if (_mainViewModel?.SelectedTabIndex == ShellTabs.History)
            {
                _mainViewModel.HistoryManager.Reload();
            }
        });

    private void OnCleanupModeChanged(object? sender, OcrCleanupMode mode)
    {
        AppSettings settings = _serviceProvider!.GetRequiredService<AppSettings>();
        settings.CleanupMode = mode;
        FileLogger.Info($"Cleanup mode set to {mode}.");
        _trayIconService?.UpdateCleanupMode(mode);
        _mainViewModel?.RefreshStatus();
        _ = SaveCleanupModeAsync(settings);
    }

    private async Task SaveCleanupModeAsync(AppSettings settings)
    {
        bool persisted = await _serviceProvider!
            .GetRequiredService<ISettingsStore>()
            .SaveAsync(settings)
            .ConfigureAwait(true);
        if (!persisted)
        {
            ILocalizationManager localization = _serviceProvider!.GetRequiredService<ILocalizationManager>();
            _trayIconService?.Notify(localization["AppTitle"], localization["Persist.SaveFailed"]);
            FileLogger.Warn("Cleanup mode changed in memory but could not be persisted.");
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        ShowShellWindow();
        _mainViewModel?.ShowSettingsTab();
    }

    private void OnSettingsSaved(object? sender, EventArgs e)
    {
        AppSettings settings = _serviceProvider!.GetRequiredService<AppSettings>();
        _trayIconService?.UpdateCleanupMode(settings.CleanupMode);
        _mainViewModel?.RefreshStatus();
    }

    private void OnAboutRequested(object? sender, EventArgs e)
    {
        ShowShellWindow();
        _mainViewModel?.ShowAboutTab();
    }

    private void OnHotkeyPressed(object? sender, string id)
    {
        switch (id)
        {
            case AppConstants.CaptureHotkeyId:
                _ = _captureCoordinator!.CaptureAsync();
                break;
            case AppConstants.InjectHotkeyId:
                _ = _injectionCoordinator!.InjectLastTextAsync();
                break;
            case AppConstants.ClipboardInjectHotkeyId:
                _ = _clipboardInjectionCoordinator!.InjectClipboardAsync();
                break;
            case AppConstants.AbortHotkeyId:
                _injectionCoordinator!.Abort();
                break;
            case AppConstants.InjectReference100HotkeyId:
                _ = _injectionCoordinator!.InjectReferenceAsync(AppConstants.InjectionReferenceShortLength);
                break;
            case AppConstants.InjectReference500HotkeyId:
                _ = _injectionCoordinator!.InjectReferenceAsync(AppConstants.InjectionReferenceMediumLength);
                break;
            case AppConstants.InjectReference1000HotkeyId:
            case AppConstants.InjectReference1000AltHotkeyId:
                _ = _injectionCoordinator!.InjectReferenceAsync(AppConstants.InjectionReferenceLongLength);
                break;
            case AppConstants.ToggleInjectionModeHotkeyId:
                _injectionCoordinator!.ToggleMode();
                break;
            case AppConstants.CycleInjectionPacingHotkeyId:
                _injectionCoordinator!.CyclePacing();
                break;
            case AppConstants.PaletteHotkeyId:
                _snippetCoordinator!.ShowPalette();
                break;
            case AppConstants.SecretPaletteHotkeyId:
                _secretCoordinator!.ShowPalette();
                break;
            case AppConstants.CaptureHistoryHotkeyId:
                _historyCoordinator!.ShowPalette();
                break;
            default:
                break;
        }
    }

    /// <summary>Builds the JSON settings store backed by the per-user app data directory.</summary>
    private static ISettingsStore BuildSettingsStore()
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppConstants.AppName,
            AppConstants.SettingsFileName);
        return new JsonSettingsStore(path);
    }

    /// <summary>Merges the theme dictionary identified by <paramref name="themeId"/> into application resources.</summary>
    private static void ApplyTheme(string themeId)
    {
        ResourceDictionary theme = new()
        {
            Source = new Uri(ThemeDictionaryUri, UriKind.Absolute),
        };

        Current.Resources.MergedDictionaries.Add(theme);
        WindowTheming.RegisterDarkTitleBarForWindows();
        FileLogger.Info($"Theme applied: '{themeId}'.");
    }

    /// <summary>Routes unhandled exceptions from every relevant source into the file logger.</summary>
    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        => FileLogger.Error("Unhandled dispatcher exception.", e.Exception);

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            FileLogger.Error("Unhandled AppDomain exception.", exception);
        }
        else
        {
            FileLogger.Error("Unhandled AppDomain exception with non-exception payload.");
        }

        FileLogger.Flush();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        FileLogger.Error("Unobserved task exception.", e.Exception);
        e.SetObserved();
    }
}
