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

using Scrybe.App.Interop;
using Scrybe.Core;
using Scrybe.Core.Input;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;

namespace Scrybe.App.Services;

/// <summary>Guards and paces typing into a confirmed target, always releasing strategy state.</summary>
public abstract class KeystrokeInjectorBase : IKeystrokeInjector
{
    private readonly AppSettings _settings;

    /// <summary>Initializes pacing settings.</summary>
    /// <param name="settings">Live pacing settings.</param>
    protected KeystrokeInjectorBase(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    /// <summary>Result of a single native batch.</summary>
    /// <param name="EventsSent">Successfully sent events.</param>
    /// <param name="Unresolved">Whether mapping was unavailable.</param>
    /// <param name="Failed">Whether native sending failed.</param>
    protected readonly record struct StrokeResult(int EventsSent, bool Unresolved, bool Failed)
    {
        /// <summary>Creates a successful result.</summary>
        /// <param name="count">Sent event count.</param>
        public static StrokeResult Sent(int count) => new(count, false, false);
        /// <summary>An unavailable character mapping.</summary>
        public static StrokeResult Skipped { get; } = new(0, true, false);
        /// <summary>A native sending failure.</summary>
        public static StrokeResult Failure { get; } = new(0, false, true);
    }

    /// <inheritdoc />
    public Task<InjectionResult> InjectAsync(KeystrokeSequence sequence, CancellationToken cancellationToken = default, IInjectionContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        return RunAsync(sequence, default, context, cancellationToken);
    }

    /// <inheritdoc />
    public Task<InjectionResult> InjectAsync(ReadOnlyMemory<char> text, CancellationToken cancellationToken = default, IInjectionContext? context = null)
        => RunAsync(null, text, context, cancellationToken);

    private async Task<InjectionResult> RunAsync(KeystrokeSequence? sequence, ReadOnlyMemory<char> text, IInjectionContext? context, CancellationToken token)
    {
        int sent = 0;
        bool started = false;
        bool verbatim = sequence is null;
        try
        {
            token.ThrowIfCancellationRequested();
            if (context is null || !context.IsCurrent)
            {
                return TargetChanged(sent);
            }

            if (IsHigherIntegrity())
            {
                return new InjectionResult(false, 0, true, false, InjectionFailureReason.HigherIntegrity);
            }

            started = true;
            BeginInjection(context);
            if (verbatim && !CanRepresentText(text.Span))
            {
                FileLogger.Error("Verbatim injection blocked before typing: unmappable characters.");
                return new InjectionResult(false, 0, false, false, InjectionFailureReason.Unmappable);
            }

            ReleaseModifiers();
            await DelayAsync(AppConstants.InjectionStartDelayMs, token).ConfigureAwait(false);
            if (!context.IsCurrent)
            {
                return TargetChanged(sent);
            }

            int count = sequence?.Strokes.Count ?? text.Length;
            context.ReportProgress(0, count);
            for (int index = 0; index < count; index++)
            {
                token.ThrowIfCancellationRequested();
                KeyStroke? stroke;
                if (sequence is not null)
                {
                    stroke = sequence.Strokes[index];
                }
                else if (!KeystrokeBuilder.TryBuildStroke(text.Span[index], out stroke, out _))
                {
                    continue;
                }

                if (!context.IsCurrent)
                {
                    return TargetChanged(sent);
                }

                StrokeResult result = SendStroke(stroke!);
                sent += result.EventsSent;
                context.ReportProgress(index + (result.Failed ? 0 : 1), count);
                if (result.Failed || (verbatim && result.Unresolved))
                {
                    return new InjectionResult(false, sent, false, false, result.Unresolved ? InjectionFailureReason.Unmappable : InjectionFailureReason.NativeFailure);
                }

                if (result.Unresolved)
                {
                    FileLogger.Warn("Unrepresentable character skipped during best-effort injection.");
                    continue;
                }

                int delay = (context.Profile?.KeyDelayMs ?? _settings.InjectionKeyDelayMs)
                    + (stroke!.Special == SpecialKey.Enter ? (context.Profile?.EnterExtraDelayMs ?? _settings.InjectionEnterExtraDelayMs) : 0);
                await DelayAsync(delay, token).ConfigureAwait(false);
                // Includes focus changes caused by Tab/Enter and the last batch of the sequence.
                if (!context.IsCurrent)
                {
                    return TargetChanged(sent);
                }
            }

            return new InjectionResult(true, sent, false, false);
        }
        catch (OperationCanceledException)
        {
            return new InjectionResult(false, sent, false, true, InjectionFailureReason.Cancelled);
        }
        finally
        {
            if (started)
            {
                try { EndInjection(); }
                finally { ReleaseModifiers(); }
            }
        }
    }

    private bool CanRepresentText(ReadOnlySpan<char> text)
    {
        foreach (char character in text)
        {
            if (!KeystrokeBuilder.TryBuildStroke(character, out KeyStroke? stroke, out bool skipped))
            {
                if (skipped) { return false; }
            }
            else if (!CanRepresentStroke(stroke!)) { return false; }
        }
        return true;
    }

    private static InjectionResult TargetChanged(int sent)
    {
        FileLogger.Warn("Injection aborted: the confirmed target or its layout changed.");
        return new InjectionResult(false, sent, false, true, InjectionFailureReason.TargetChanged);
    }

    /// <summary>Checks elevation without changing the target.</summary>
    protected virtual bool IsHigherIntegrity() => InjectionInterop.IsForegroundHigherIntegrity();
    /// <summary>Releases modifier keys at entry and during guaranteed cleanup.</summary>
    protected virtual void ReleaseModifiers() => InjectionInterop.ReleaseModifiers();
    /// <summary>Waits between batches; overridden by deterministic tests.</summary>
    /// <param name="milliseconds">Delay duration.</param>
    /// <param name="token">Emergency abort token.</param>
    protected virtual Task DelayAsync(int milliseconds, CancellationToken token) => Task.Delay(milliseconds, token);
    /// <summary>Initializes strategy state before preflight.</summary>
    /// <param name="context">Confirmed target and layout snapshot.</param>
    protected virtual void BeginInjection(IInjectionContext context) { }
    /// <summary>Releases retained strategy state on all exit paths.</summary>
    protected virtual void EndInjection() { }
    /// <summary>Preflights a character using the same mapping as sending.</summary>
    /// <param name="stroke">Stroke to validate.</param>
    protected virtual bool CanRepresentStroke(KeyStroke stroke) => true;
    /// <summary>Sends one adjacent batch of native events.</summary>
    /// <param name="stroke">Stroke to send.</param>
    protected abstract StrokeResult SendStroke(KeyStroke stroke);
}
