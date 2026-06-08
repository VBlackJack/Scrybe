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

/// <summary>
/// Shared injection loop for both strategies: the UIPI guard, the start-of-injection modifier release
/// and settle delay, the paced per-stroke send, the emergency-abort handling, and the result. Strategy
/// subclasses only implement how a single keystroke becomes Win32 events.
/// </summary>
public abstract class KeystrokeInjectorBase : IKeystrokeInjector
{
    private readonly AppSettings _settings;

    /// <summary>Initializes the base injector with the settings that control pacing.</summary>
    /// <param name="settings">Application settings holding the keystroke delays.</param>
    protected KeystrokeInjectorBase(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    /// <summary>The outcome of sending a single keystroke's events.</summary>
    /// <param name="EventsSent">Number of Win32 events injected.</param>
    /// <param name="Unresolved">Whether the character could not be represented and was skipped.</param>
    /// <param name="Failed">Whether the underlying <c>SendInput</c> call failed.</param>
    protected readonly record struct StrokeResult(int EventsSent, bool Unresolved, bool Failed)
    {
        /// <summary>A successful send of <paramref name="count"/> events.</summary>
        /// <param name="count">The number of events sent.</param>
        public static StrokeResult Sent(int count) => new(count, Unresolved: false, Failed: false);

        /// <summary>A character that could not be represented and was skipped.</summary>
        public static StrokeResult Skipped { get; } = new(0, Unresolved: true, Failed: false);

        /// <summary>A failed injection.</summary>
        public static StrokeResult Failure { get; } = new(0, Unresolved: false, Failed: true);
    }

    /// <inheritdoc />
    public async Task<InjectionResult> InjectAsync(KeystrokeSequence sequence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        if (InjectionInterop.IsForegroundHigherIntegrity())
        {
            FileLogger.Warn("Injection blocked by UIPI: the focused window runs at a higher integrity level than Scrybe.");
            return new InjectionResult(Success: false, KeystrokesSent: 0, UipiBlocked: true, Aborted: false);
        }

        InjectionInterop.ReleaseModifiers();
        try
        {
            await Task.Delay(AppConstants.InjectionStartDelayMs, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new InjectionResult(Success: false, KeystrokesSent: 0, UipiBlocked: false, Aborted: true);
        }

        BeginInjection();

        int sent = 0;
        int unresolved = 0;
        bool failed = false;

        try
        {
            foreach (KeyStroke stroke in sequence.Strokes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                StrokeResult result = SendStroke(stroke);
                if (result.Failed)
                {
                    failed = true;
                    break;
                }

                if (result.Unresolved)
                {
                    unresolved++;
                    continue;
                }

                sent += result.EventsSent;

                int delay = _settings.InjectionKeyDelayMs
                    + (stroke.Special == SpecialKey.Enter ? _settings.InjectionEnterExtraDelayMs : 0);
                if (delay > 0)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            EndInjection();
            InjectionInterop.ReleaseModifiers();
            FileLogger.Info($"Injection aborted after {sent} key events; modifiers released.");
            return new InjectionResult(Success: false, KeystrokesSent: sent, UipiBlocked: false, Aborted: true);
        }

        EndInjection();

        if (failed)
        {
            InjectionInterop.ReleaseModifiers();
            FileLogger.Error("SendInput injected no events; aborting injection.");
            return new InjectionResult(Success: false, KeystrokesSent: sent, UipiBlocked: false, Aborted: false);
        }

        if (unresolved > 0)
        {
            FileLogger.Warn($"{unresolved} characters could not be represented in {GetType().Name} and were skipped.");
        }

        return new InjectionResult(Success: true, KeystrokesSent: sent, UipiBlocked: false, Aborted: false);
    }

    /// <inheritdoc />
    public async Task<InjectionResult> InjectAsync(ReadOnlyMemory<char> text, CancellationToken cancellationToken = default)
    {
        if (InjectionInterop.IsForegroundHigherIntegrity())
        {
            FileLogger.Warn("Injection blocked by UIPI: the focused window runs at a higher integrity level than Scrybe.");
            return new InjectionResult(Success: false, KeystrokesSent: 0, UipiBlocked: true, Aborted: false);
        }

        InjectionInterop.ReleaseModifiers();
        try
        {
            await Task.Delay(AppConstants.InjectionStartDelayMs, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new InjectionResult(Success: false, KeystrokesSent: 0, UipiBlocked: false, Aborted: true);
        }

        BeginInjection();

        int sent = 0;
        int unresolved = 0;
        bool failed = false;

        try
        {
            for (int index = 0; index < text.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                char character = text.Span[index];
                if (!KeystrokeBuilder.TryBuildStroke(character, out KeyStroke? stroke, out bool skipped))
                {
                    if (skipped)
                    {
                        unresolved++;
                    }

                    continue;
                }

                StrokeResult result = SendStroke(stroke!);
                if (result.Failed)
                {
                    failed = true;
                    break;
                }

                if (result.Unresolved)
                {
                    unresolved++;
                    continue;
                }

                sent += result.EventsSent;

                int delay = _settings.InjectionKeyDelayMs
                    + (stroke!.Special == SpecialKey.Enter ? _settings.InjectionEnterExtraDelayMs : 0);
                if (delay > 0)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            EndInjection();
            InjectionInterop.ReleaseModifiers();
            FileLogger.Info($"Injection aborted after {sent} key events; modifiers released.");
            return new InjectionResult(Success: false, KeystrokesSent: sent, UipiBlocked: false, Aborted: true);
        }

        EndInjection();

        if (failed)
        {
            InjectionInterop.ReleaseModifiers();
            FileLogger.Error("SendInput injected no events; aborting injection.");
            return new InjectionResult(Success: false, KeystrokesSent: sent, UipiBlocked: false, Aborted: false);
        }

        if (unresolved > 0)
        {
            FileLogger.Warn($"{unresolved} characters could not be represented in {GetType().Name} and were skipped.");
        }

        return new InjectionResult(Success: true, KeystrokesSent: sent, UipiBlocked: false, Aborted: false);
    }

    /// <summary>Resets per-injection state before the first keystroke. Default: no-op.</summary>
    protected virtual void BeginInjection()
    {
    }

    /// <summary>Releases any state held across keystrokes (for example a held Shift). Default: no-op.</summary>
    protected virtual void EndInjection()
    {
    }

    /// <summary>Sends the Win32 events for a single keystroke.</summary>
    /// <param name="stroke">The keystroke to send.</param>
    protected abstract StrokeResult SendStroke(KeyStroke stroke);
}
