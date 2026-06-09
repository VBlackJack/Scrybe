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
using System.Globalization;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Scrybe.App.Imaging;
using Scrybe.App.Views;
using Scrybe.Core.Imaging;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;
using Scrybe.Core.Text;

namespace Scrybe.App.Services;

/// <summary>
/// Orchestrates the full capture-to-clipboard flow: grab the primary monitor into a frozen frame,
/// show the selection overlay, OCR the chosen region, copy the recognized text to the clipboard, and
/// show a brief confirmation. A single capture runs at a time; failures are logged. Win32/WPF concerns
/// are kept out of Core and the view models.
/// </summary>
public sealed class CaptureCoordinator
{
    private readonly IScreenCaptureService _captureService;
    private readonly IOcrEngine _ocrEngine;
    private readonly IClipboardService _clipboard;
    private readonly IOcrTextStore _textStore;
    private readonly CaptureHistoryLibrary _historyLibrary;
    private readonly INotificationService _notification;
    private readonly ILocalizationManager _localization;
    private readonly AppSettings _settings;
    private readonly Dispatcher _dispatcher;

    private int _captureInProgress;

    /// <summary>Initializes the coordinator with its capture, OCR, clipboard and notification dependencies.</summary>
    /// <param name="captureService">Service that captures the primary monitor into a frame.</param>
    /// <param name="ocrEngine">OCR engine used to recognize the selected region.</param>
    /// <param name="clipboard">Clipboard service for the recognized text.</param>
    /// <param name="textStore">Store that retains the recognized text for the injection flow.</param>
    /// <param name="historyLibrary">Protected OCR capture history library.</param>
    /// <param name="notification">Notification service for the confirmation balloon.</param>
    /// <param name="localization">Source of localized overlay and notification text.</param>
    /// <param name="settings">Application settings holding the captures directory and flags.</param>
    public CaptureCoordinator(
        IScreenCaptureService captureService,
        IOcrEngine ocrEngine,
        IClipboardService clipboard,
        IOcrTextStore textStore,
        CaptureHistoryLibrary historyLibrary,
        INotificationService notification,
        ILocalizationManager localization,
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(captureService);
        ArgumentNullException.ThrowIfNull(ocrEngine);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(textStore);
        ArgumentNullException.ThrowIfNull(historyLibrary);
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(settings);

        _captureService = captureService;
        _ocrEngine = ocrEngine;
        _clipboard = clipboard;
        _textStore = textStore;
        _historyLibrary = historyLibrary;
        _notification = notification;
        _localization = localization;
        _settings = settings;
        _dispatcher = System.Windows.Application.Current.Dispatcher;
    }

    /// <summary>
    /// Runs the full capture-to-clipboard flow. Concurrent triggers are ignored while one is in
    /// progress. Never throws: failures are logged.
    /// </summary>
    public async Task CaptureAsync()
    {
        if (Interlocked.CompareExchange(ref _captureInProgress, 1, 0) != 0)
        {
            FileLogger.Warn("Capture already in progress; ignoring trigger.");
            return;
        }

        try
        {
            FileLogger.Info("Capture triggered.");
            long startTimestamp = Stopwatch.GetTimestamp();

            CapturedFrame frame = await _captureService
                .CapturePrimaryMonitorFrameAsync()
                .ConfigureAwait(false);

            OverlayOutcome outcome = await ShowOverlayAsync(frame, startTimestamp).ConfigureAwait(false);

            if (outcome.Selection is null || outcome.Frozen is null)
            {
                FileLogger.Info("Region capture cancelled.");
                return;
            }

            PixelBuffer crop = PixelBuffer.FromFrameRegion(frame, outcome.Selection);

            long preprocessStart = Stopwatch.GetTimestamp();
            PixelBuffer ocrInput;
            bool inverted = false;
            int upscaleFactor = 1;
            if (_settings.EnablePreprocessing)
            {
                PreprocessingResult preprocessed = ImagePreprocessor.Process(crop, PreprocessingOptions.Default);
                ocrInput = preprocessed.Output;
                inverted = preprocessed.Inverted;
                upscaleFactor = preprocessed.AppliedUpscaleFactor;
            }
            else
            {
                ocrInput = crop;
            }

            double preprocessMs = Stopwatch.GetElapsedTime(preprocessStart).TotalMilliseconds;
            byte[] pngBytes = FrameImaging.EncodePixelBuffer(ocrInput);

            long ocrStart = Stopwatch.GetTimestamp();
            OcrResult recognized = await _ocrEngine.RecognizeAsync(pngBytes).ConfigureAwait(false);
            double ocrMs = Stopwatch.GetElapsedTime(ocrStart).TotalMilliseconds;

            string text = recognized.Text.Trim();
            TextPostProcessingResult postProcessed = TextPostProcessor.Process(
                text, TextPostProcessingOptions.ForMode(_settings.CleanupMode));
            text = postProcessed.Text.Trim();
            FileLogger.Info(
                $"Cleanup ({_settings.CleanupMode}): merged {postProcessed.MergedLines}, "
                + $"prompts {postProcessed.StrippedPrompts}, logs {postProcessed.StrippedLogDecorations}, "
                + $"corrected {postProcessed.CorrectedTokens}.");

            _textStore.Set(text);
            await _clipboard.SetTextAsync(text).ConfigureAwait(false);

            if (_settings.EnableCaptureHistory && !string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    bool persistedHistory = await _historyLibrary.AddAsync(text).ConfigureAwait(false);
                    if (!persistedHistory)
                    {
                        _notification.Notify(_localization["AppTitle"], _localization["Persist.SaveFailed"]);
                    }
                }
                catch (Exception exception)
                {
                    FileLogger.Error("Failed to record capture history.", exception);
                }
            }

            if (_settings.SaveCaptureCrop)
            {
                FrameImaging.SaveCrop(outcome.Frozen, outcome.Selection, _settings.CapturesDirectory);
            }

            NotifyCopied(text.Length);

            double totalMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            FileLogger.Info(
                $"OCR -> clipboard: {text.Length} chars, confidence {recognized.MeanConfidence:F1}, " +
                $"polarity {(inverted ? "inverted" : "kept")}, upscale {upscaleFactor}x, " +
                $"preprocess {preprocessMs:F0}ms, ocr {ocrMs:F0}ms, total {totalMs:F0}ms.");
        }
        catch (Exception exception)
        {
            FileLogger.Error("Capture-to-clipboard failed.", exception);
            _notification.Notify(_localization["AppTitle"], _localization["Notify.CaptureFailed"]);
        }
        finally
        {
            Interlocked.Exchange(ref _captureInProgress, 0);
        }
    }

    private void NotifyCopied(int characterCount)
    {
        string title = _localization["AppTitle"];
        string message = string.Format(
            CultureInfo.CurrentCulture, _localization["Notify.Copied"], characterCount);
        _notification.Notify(title, message);
    }

    private Task<OverlayOutcome> ShowOverlayAsync(CapturedFrame frame, long startTimestamp)
    {
        TaskCompletionSource<OverlayOutcome> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        _dispatcher.InvokeAsync(() =>
        {
            try
            {
                BitmapSource frozen = FrameImaging.ToFrozenBitmap(frame);
                CaptureOverlayWindow overlay = new(frozen, frame, _localization);
                overlay.SelectionCompleted += (_, selection) =>
                    completion.TrySetResult(new OverlayOutcome(selection, frozen));
                overlay.Closed += (_, _) =>
                    completion.TrySetResult(new OverlayOutcome(null, null));

                overlay.Show();
                overlay.Activate();

                double latencyMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                FileLogger.Info($"Capture overlay shown in {latencyMs:F0}ms.");
            }
            catch (Exception exception)
            {
                FileLogger.Error("Failed to show capture overlay.", exception);
                _notification.Notify(_localization["AppTitle"], _localization["Notify.CaptureFailed"]);
                completion.TrySetResult(new OverlayOutcome(null, null));
            }
        });

        return completion.Task;
    }

    private sealed record OverlayOutcome(PixelRect? Selection, BitmapSource? Frozen);
}
