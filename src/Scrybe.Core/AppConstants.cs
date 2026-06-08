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

namespace Scrybe.Core;

/// <summary>
/// Central, immutable application constants. Every value that would otherwise be a
/// magic string, magic number, or hardcoded path lives here so it can be changed in
/// exactly one place.
/// </summary>
public static class AppConstants
{
    /// <summary>Canonical product name, used in log file names and window chrome.</summary>
    public const string AppName = "Scrybe";

    /// <summary>Name of the sub-directory (under the application base directory) that holds daily log files.</summary>
    public const string LogSubDirName = "logs";

    /// <summary>Name of the sub-directory (under the application base directory) that holds JSON locale files.</summary>
    public const string LocalesDirName = "locales";

    /// <summary>Identifier of the theme applied by default at startup.</summary>
    public const string DefaultThemeId = "dracula-dark";

    /// <summary>Locale code loaded when no user preference is available.</summary>
    public const string DefaultLocaleCode = "en";

    /// <summary>Interval, in milliseconds, at which the background logger drains its queue to disk.</summary>
    public const int LogFlushIntervalMs = 2000;

    /// <summary>File extension (including the dot) used for daily log files.</summary>
    public const string LogFileExtension = ".log";

    /// <summary>File extension (including the dot) used for JSON locale files.</summary>
    public const string LocaleFileExtension = ".json";

    /// <summary>Default global hotkey modifiers, expressed as a parseable string (for example <c>Control+Alt</c>).</summary>
    public const string DefaultHotkeyModifiers = "Control+Alt";

    /// <summary>Default global hotkey key, expressed as a parseable token (for example <c>S</c>).</summary>
    public const string DefaultHotkeyKey = "S";

    /// <summary>Name of the sub-directory (under <c>%LOCALAPPDATA%/Scrybe</c>) that stores captured images.</summary>
    public const string CapturesSubDirName = "captures";

    /// <summary>Prefix applied to every captured PNG file name.</summary>
    public const string CaptureFileNamePrefix = "Scrybe_capture_";

    /// <summary>Timestamp format embedded in captured PNG file names.</summary>
    public const string CaptureFileTimestampFormat = "yyyyMMdd_HHmmss_fff";

    /// <summary>File extension (including the dot) used for captured images.</summary>
    public const string CaptureFileExtension = ".png";

    /// <summary>Reference DPI at which one device-independent pixel equals one physical pixel (100% scaling).</summary>
    public const double DipBaseline = 96.0;

    /// <summary>Minimum width and height, in physical pixels, for a selection to be considered valid.</summary>
    public const int MinSelectionPhysicalPixels = 8;

    /// <summary>Name of the directory (next to the executable) holding the bundled Tesseract traineddata.</summary>
    public const string TessdataDirName = "tessdata";

    /// <summary>Default OCR language code (matches the bundled <c>eng.traineddata</c>).</summary>
    public const string DefaultOcrLanguage = "eng";

    /// <summary>Number of attempts to set the clipboard before giving up.</summary>
    public const int ClipboardRetryCount = 3;

    /// <summary>Delay, in milliseconds, between clipboard retry attempts.</summary>
    public const int ClipboardRetryDelayMs = 80;

    /// <summary>Integer factor by which small text is upscaled before OCR.</summary>
    public const int PreprocessUpscaleFactor = 2;

    /// <summary>Estimated text line height (physical pixels) at or below which upscaling is applied.</summary>
    public const int PreprocessMinLineHeightPx = 22;

    /// <summary>Luminance (0–255) at or below which a grayscale pixel counts as "ink" for line-height estimation.</summary>
    public const int PreprocessInkLuminanceThreshold = 128;

    /// <summary>Minimum fraction of a row that must be ink for the row to count as a text row.</summary>
    public const double PreprocessRowInkMinFraction = 0.02;

    /// <summary>
    /// Mean border luminance (0–255) below which the crop is judged light-on-dark and inverted so the
    /// OCR engine (trained mostly on dark-on-light) sees its expected polarity.
    /// </summary>
    public const double PreprocessInversionLuminanceThreshold = 128.0;

    /// <summary>
    /// Whether the pipeline hard-binarizes (single-pass Otsu) before OCR. Default <see langword="false"/>:
    /// the empirical before/after test showed Tesseract 5's LSTM recognizes the contrast-normalized
    /// grayscale better than a binarized image (binarization clips the antialiased thin strokes the
    /// LSTM relies on, e.g. the digits and dots of an IP address). Kept available for legacy needs.
    /// </summary>
    public const bool PreprocessApplyBinarization = false;

    /// <summary>
    /// Leading shell-prompt patterns stripped from line starts (anchored, most specific first):
    /// PowerShell, user@host:path with # or $, then bare $/#/&gt; prompts.
    /// </summary>
    public static readonly string[] OcrPromptPatterns =
    {
        @"^PS [A-Za-z]:\\[^>]*>\s?",
        @"^[\w.-]+@[\w.-]+:[^#$]*[#$]\s?",
        @"^\$\s",
        @"^#\s",
        @"^>\s",
    };

    /// <summary>Matches a <c>key:</c> style line start, which denotes a new logical line (never a wrap continuation).</summary>
    public const string OcrKeyValueStartPattern = @"^[A-Za-z][\w.-]*:(\s|$)";

    /// <summary>Matches a bullet line start, which denotes a new logical line.</summary>
    public const string OcrBulletStartPattern = @"^[-*•+]\s";

    /// <summary>Shape of an IPv4 / IP:port token, tolerating the OCR confusions that map to digits.</summary>
    public const string OcrIpTokenPattern = @"^[0-9OoIl@]{1,3}(\.[0-9OoIl@]{1,3}){3}(:[0-9OoIl]{1,5})?$";

    /// <summary>Shape of a canonical GUID token, tolerating hex OCR confusions.</summary>
    public const string OcrGuidTokenPattern =
        @"^[0-9A-Fa-fOoIlSZ]{8}-[0-9A-Fa-fOoIlSZ]{4}-[0-9A-Fa-fOoIlSZ]{4}-[0-9A-Fa-fOoIlSZ]{4}-[0-9A-Fa-fOoIlSZ]{12}$";

    /// <summary>Shape of a long hex/hash token (minimum 8 chars), tolerating hex OCR confusions.</summary>
    public const string OcrHexTokenPattern = @"^[0-9A-Fa-fOoIlSZ]{8,}$";

    /// <summary>Shape of a numeric token, tolerating letter→digit OCR confusions.</summary>
    public const string OcrNumericTokenPattern = @"^[0-9OoIlSZB]+$";

    /// <summary>
    /// Anchored leading-timestamp patterns stripped by Log Cleaner: ISO, syslog and bracketed time.
    /// All anchored to line start so a time-like substring elsewhere (paths, prose) is never touched.
    /// </summary>
    public static readonly string[] OcrTimestampPatterns =
    {
        @"^\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?\s*",
        @"^[A-Z][a-z]{2}\s+\d{1,2}\s+\d{2}:\d{2}:\d{2}\s*",
        @"^\[\d{2}:\d{2}:\d{2}(?:\.\d+)?\]\s*",
    };

    /// <summary>Anchored leading log-level prefix (optionally bracketed), stripped by Log Cleaner.</summary>
    public const string OcrLogLevelPattern = @"^\[?(?:INFO|WARNING|WARN|ERROR|DEBUG|TRACE)\]?[:\s]\s*";

    /// <summary>
    /// Anchored PID prefix in clear log position: <c>name[1234]:</c> → <c>name:</c>. Requires a name,
    /// bracketed digits and a trailing colon, so a bare bracketed number elsewhere is never altered.
    /// </summary>
    public const string OcrPidPattern = @"^([\w.-]+)\[\d+\](:)";

    /// <summary>Identifier for the capture hotkey registration.</summary>
    public const string CaptureHotkeyId = "capture";

    /// <summary>Identifier for the inject-last-text hotkey registration.</summary>
    public const string InjectHotkeyId = "inject";

    /// <summary>Identifier for the emergency-abort hotkey registration.</summary>
    public const string AbortHotkeyId = "abort";

    /// <summary>Default modifiers for the inject-last-text hotkey.</summary>
    public const string DefaultInjectHotkeyModifiers = "Control+Alt";

    /// <summary>Default key for the inject-last-text hotkey.</summary>
    public const string DefaultInjectHotkeyKey = "V";

    /// <summary>Default modifiers for the emergency-abort hotkey.</summary>
    public const string DefaultAbortHotkeyModifiers = "Control+Alt";

    /// <summary>Default key for the emergency-abort hotkey.</summary>
    public const string DefaultAbortHotkeyKey = "Q";

    /// <summary>Base delay, in milliseconds, between injected keystrokes (both local apps and remote consoles drop fast input).</summary>
    public const int InjectionKeyDelayMs = 25;

    /// <summary>Additional delay, in milliseconds, after an injected Enter (consoles need time to process a line).</summary>
    public const int InjectionEnterExtraDelayMs = 40;

    /// <summary>Delay, in milliseconds, after releasing modifiers and before typing, so the trigger chord settles.</summary>
    public const int InjectionStartDelayMs = 300;

    /// <summary>Set-1 hardware scancode for the Enter key.</summary>
    public const ushort EnterScanCode = 0x1C;

    /// <summary>Set-1 hardware scancode for the Tab key.</summary>
    public const ushort TabScanCode = 0x0F;

    /// <summary>Set-1 hardware scancode for the Left Shift key.</summary>
    public const ushort LeftShiftScanCode = 0x2A;

    /// <summary>Virtual-key code for Enter, used by Unicode injection (which cannot send Enter as a codepoint).</summary>
    public const ushort VkReturn = 0x0D;

    /// <summary>Virtual-key code for Tab, used by Unicode injection.</summary>
    public const ushort VkTab = 0x09;

    /// <summary>
    /// Repeating pattern for the integrity reference string (no AltGr-only symbols, so both modes can
    /// type it). Mixes lower/upper case, digits, common symbols, spaces and newlines.
    /// </summary>
    public const string InjectionReferencePattern =
        "Status: bind 0.0.0.0:8443 failed (code=13), retry Agent now\nPath /usr/bin v2 ID7-OK done; next\n";

    /// <summary>Reference length for the short integrity test.</summary>
    public const int InjectionReferenceShortLength = 100;

    /// <summary>Reference length for the medium integrity test.</summary>
    public const int InjectionReferenceMediumLength = 500;

    /// <summary>Reference length for the long integrity test.</summary>
    public const int InjectionReferenceLongLength = 1000;

    /// <summary>Identifiers for the debug reference-injection and mode-toggle hotkeys.</summary>
    public const string InjectReference100HotkeyId = "inject-ref-100";

    /// <summary>Identifier for the 500-char reference-injection hotkey.</summary>
    public const string InjectReference500HotkeyId = "inject-ref-500";

    /// <summary>Identifier for the 1000-char reference-injection hotkey.</summary>
    public const string InjectReference1000HotkeyId = "inject-ref-1000";

    /// <summary>Alternate identifier for the 1000-char reference-injection hotkey.</summary>
    public const string InjectReference1000AltHotkeyId = "inject-ref-1000-alt";

    /// <summary>Identifier for the injection-mode toggle hotkey.</summary>
    public const string ToggleInjectionModeHotkeyId = "toggle-mode";

    /// <summary>Identifier for the debug injection pacing-cycle hotkey.</summary>
    public const string CycleInjectionPacingHotkeyId = "cycle-pacing";

    /// <summary>Default modifiers for the debug injection hotkeys.</summary>
    public const string DefaultDebugHotkeyModifiers = "Control+Alt";

    /// <summary>Default key for the 100-char reference-injection hotkey.</summary>
    public const string DefaultInjectReference100Key = "F1";

    /// <summary>Default key for the 500-char reference-injection hotkey.</summary>
    public const string DefaultInjectReference500Key = "F2";

    /// <summary>Default key for the 1000-char reference-injection hotkey.</summary>
    public const string DefaultInjectReference1000Key = "F3";

    /// <summary>Alternate default key for the 1000-char reference-injection hotkey.</summary>
    public const string DefaultInjectReference1000AltKey = "F6";

    /// <summary>Default key for the injection-mode toggle hotkey.</summary>
    public const string DefaultToggleInjectionModeKey = "F4";

    /// <summary>Default key for cycling debug injection pacing presets.</summary>
    public const string DefaultCycleInjectionPacingKey = "F5";

    /// <summary>Debug pacing presets, in milliseconds, for remote integrity A/B testing.</summary>
    public static readonly int[] DebugInjectionPacingPresetsMs = { 10, 25, 50, 100 };

    /// <summary>
    /// Regex matching a snippet placeholder or an escaped literal brace pair: <c>{{{{</c> → literal
    /// <c>{{</c>, <c>}}}}</c> → literal <c>}}</c>, and <c>{{name}}</c> → the captured parameter name.
    /// </summary>
    public const string SnippetPlaceholderPattern = @"\{\{\{\{|\}\}\}\}|\{\{(\w+)\}\}";

    /// <summary>Escaped opening braces that render as a literal <c>{{</c>.</summary>
    public const string SnippetEscapedOpen = "{{{{";

    /// <summary>Escaped closing braces that render as a literal <c>}}</c>.</summary>
    public const string SnippetEscapedClose = "}}}}";

    /// <summary>Literal opening braces produced by an escape.</summary>
    public const string SnippetLiteralOpen = "{{";

    /// <summary>Literal closing braces produced by an escape.</summary>
    public const string SnippetLiteralClose = "}}";

    /// <summary>File name (under <c>%LOCALAPPDATA%/Scrybe</c>) where non-secret snippets are stored.</summary>
    public const string SnippetsFileName = "snippets.json";

    /// <summary>File name (under <c>%LOCALAPPDATA%/Scrybe</c>) where DPAPI-protected secrets are stored.</summary>
    public const string SecretsFileName = "secrets.json";

    /// <summary>File name (under <c>%LOCALAPPDATA%/Scrybe</c>) where DPAPI-protected OCR history is stored.</summary>
    public const string CaptureHistoryFileName = "history.json";

    /// <summary>Identifier for the snippet-palette hotkey registration.</summary>
    public const string PaletteHotkeyId = "palette";

    /// <summary>Identifier for the secret-palette hotkey registration.</summary>
    public const string SecretPaletteHotkeyId = "secret-palette";

    /// <summary>Identifier for the capture-history palette hotkey registration.</summary>
    public const string CaptureHistoryHotkeyId = "history-palette";

    /// <summary>Default modifiers for the snippet-palette hotkey.</summary>
    public const string DefaultPaletteHotkeyModifiers = "Control+Alt";

    /// <summary>Default key for the snippet-palette hotkey.</summary>
    public const string DefaultPaletteHotkeyKey = "P";

    /// <summary>Default modifiers for the secret-palette hotkey.</summary>
    public const string DefaultSecretPaletteHotkeyModifiers = "Control+Alt";

    /// <summary>Default key for the secret-palette hotkey.</summary>
    public const string DefaultSecretPaletteHotkeyKey = "K";

    /// <summary>Default modifiers for the capture-history palette hotkey.</summary>
    public const string DefaultHistoryPaletteHotkeyModifiers = "Control+Alt";

    /// <summary>Default key for the capture-history palette hotkey.</summary>
    public const string DefaultHistoryPaletteHotkeyKey = "H";

    /// <summary>Default maximum number of OCR captures retained in history.</summary>
    public const int DefaultCaptureHistoryMaxEntries = 25;

    /// <summary>Minimum accepted OCR history size.</summary>
    public const int CaptureHistoryMaxEntriesMin = 1;

    /// <summary>Maximum accepted OCR history size.</summary>
    public const int CaptureHistoryMaxEntriesMax = 200;

    /// <summary>Maximum number of characters shown in a capture-history palette preview.</summary>
    public const int CaptureHistoryPreviewMaxChars = 80;

    /// <summary>Suffix appended to truncated capture-history previews.</summary>
    public const string CaptureHistoryPreviewSuffix = "...";

    /// <summary>Current-culture timestamp format used by capture-history palette entries.</summary>
    public const string CaptureHistoryTimestampFormat = "g";

    /// <summary>File name (under <c>%LOCALAPPDATA%/Scrybe</c>) where user settings are persisted.</summary>
    public const string SettingsFileName = "settings.json";

    /// <summary>Minimum accepted base inter-keystroke delay, in milliseconds (validation floor).</summary>
    public const int InjectionKeyDelayMinMs = 5;

    /// <summary>Maximum accepted base inter-keystroke delay, in milliseconds (validation ceiling for slow links).</summary>
    public const int InjectionKeyDelayMaxMs = 200;

    /// <summary>Maximum accepted extra delay after Enter, in milliseconds (validation ceiling).</summary>
    public const int InjectionEnterExtraDelayMaxMs = 1000;

    /// <summary>Locale codes shipped with the application.</summary>
    public static readonly string[] SupportedLocaleCodes = { "en", "fr" };
}
