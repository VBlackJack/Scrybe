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

namespace Scrybe.Core.Models;

/// <summary>
/// User-configurable application settings. In this increment the values are
/// in-memory defaults; persistence is introduced in a later increment.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Whether file logging is enabled. Default: <see langword="true"/>.</summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>Identifier of the theme to apply at startup.</summary>
    public string DefaultThemeId { get; set; } = AppConstants.DefaultThemeId;

    /// <summary>Locale code (for example <c>en</c> or <c>fr</c>) to load at startup.</summary>
    public string LocaleCode { get; set; } = AppConstants.DefaultLocaleCode;

    /// <summary>Global hotkey modifiers as a parseable string (for example <c>Control+Alt</c>).</summary>
    public string HotkeyModifiers { get; set; } = AppConstants.DefaultHotkeyModifiers;

    /// <summary>Global hotkey key as a parseable token (for example <c>S</c>).</summary>
    public string HotkeyKey { get; set; } = AppConstants.DefaultHotkeyKey;

    /// <summary>
    /// Directory where captured images are written. When <see langword="null"/> or empty,
    /// the resolver falls back to <c>%LOCALAPPDATA%/Scrybe/captures</c>.
    /// </summary>
    public string? CapturesDirectory { get; set; }

    /// <summary>OCR language code, matching a bundled traineddata file (default <c>eng</c>).</summary>
    public string OcrLanguage { get; set; } = AppConstants.DefaultOcrLanguage;

    /// <summary>
    /// Whether to pre-warm the capture device and OCR engine on a background thread at startup,
    /// so the first capture is fast. Default: <see langword="true"/>.
    /// </summary>
    public bool PrewarmOnStartup { get; set; } = true;

    /// <summary>
    /// Whether to also save the cropped region as a PNG (debug aid). The OCR text on the clipboard
    /// is the primary output, so this defaults to <see langword="false"/>.
    /// </summary>
    public bool SaveCaptureCrop { get; set; }

    /// <summary>
    /// Whether to run the console-aware pre-processing pipeline (grayscale, polarity inversion,
    /// upscale, contrast, binarization) before OCR. Default: <see langword="true"/>.
    /// </summary>
    public bool EnablePreprocessing { get; set; } = true;

    /// <summary>
    /// The OCR cleanup mode applied to the recognized text before the clipboard. Default
    /// <see cref="OcrCleanupMode.Standard"/>; <see cref="OcrCleanupMode.Raw"/> is a verbatim passthrough.
    /// </summary>
    public OcrCleanupMode CleanupMode { get; set; } = OcrCleanupMode.Standard;

    /// <summary>Modifiers for the hotkey that injects the last OCR text.</summary>
    public string InjectHotkeyModifiers { get; set; } = AppConstants.DefaultInjectHotkeyModifiers;

    /// <summary>Key for the hotkey that injects the last OCR text.</summary>
    public string InjectHotkeyKey { get; set; } = AppConstants.DefaultInjectHotkeyKey;

    /// <summary>Modifiers for the emergency-abort hotkey.</summary>
    public string AbortHotkeyModifiers { get; set; } = AppConstants.DefaultAbortHotkeyModifiers;

    /// <summary>Key for the emergency-abort hotkey.</summary>
    public string AbortHotkeyKey { get; set; } = AppConstants.DefaultAbortHotkeyKey;

    /// <summary>Base inter-keystroke delay, in milliseconds, used when injecting.</summary>
    public int InjectionKeyDelayMs { get; set; } = AppConstants.InjectionKeyDelayMs;

    /// <summary>Extra delay, in milliseconds, applied after an injected Enter.</summary>
    public int InjectionEnterExtraDelayMs { get; set; } = AppConstants.InjectionEnterExtraDelayMs;

    /// <summary>
    /// Active keystroke injection strategy. Default <see cref="InjectionMode.Unicode"/> (layout-independent),
    /// confirmed against <see cref="InjectionMode.Scancode"/> by the integrity A/B.
    /// </summary>
    public InjectionMode InjectionMode { get; set; } = InjectionMode.Unicode;

    /// <summary>
    /// Whether to register the debug reference-injection and mode-toggle hotkeys used to measure
    /// integrity against a known string. Intended off in release builds.
    /// </summary>
    public bool DebugInjectionEnabled { get; set; } = true;

    /// <summary>Modifiers for the snippet-palette hotkey.</summary>
    public string PaletteHotkeyModifiers { get; set; } = AppConstants.DefaultPaletteHotkeyModifiers;

    /// <summary>Key for the snippet-palette hotkey.</summary>
    public string PaletteHotkeyKey { get; set; } = AppConstants.DefaultPaletteHotkeyKey;

    /// <summary>Modifiers for the secret-palette hotkey.</summary>
    public string SecretPaletteHotkeyModifiers { get; set; } = AppConstants.DefaultSecretPaletteHotkeyModifiers;

    /// <summary>Key for the secret-palette hotkey.</summary>
    public string SecretPaletteHotkeyKey { get; set; } = AppConstants.DefaultSecretPaletteHotkeyKey;

    /// <summary>Whether OCR captures are recorded into the protected history. Default: <see langword="true"/>.</summary>
    public bool EnableCaptureHistory { get; set; } = true;

    /// <summary>Maximum number of OCR captures retained in protected history.</summary>
    public int CaptureHistoryMaxEntries { get; set; } = AppConstants.DefaultCaptureHistoryMaxEntries;

    /// <summary>Modifiers for the capture-history palette hotkey.</summary>
    public string HistoryPaletteHotkeyModifiers { get; set; } = AppConstants.DefaultHistoryPaletteHotkeyModifiers;

    /// <summary>Key for the capture-history palette hotkey.</summary>
    public string HistoryPaletteHotkeyKey { get; set; } = AppConstants.DefaultHistoryPaletteHotkeyKey;
}
