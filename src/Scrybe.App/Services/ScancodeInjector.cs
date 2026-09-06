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
using Scrybe.Core.Models;

namespace Scrybe.App.Services;

/// <summary>Maps against the confirmed target layout and sends atomic scancode batches.</summary>
public sealed class ScancodeInjector : KeystrokeInjectorBase
{
    private readonly ScancodeEventBuilder _builder = new();
    private IntPtr _layout;

    /// <summary>Initializes pacing settings.</summary>
    /// <param name="settings">Live pacing settings.</param>
    public ScancodeInjector(AppSettings settings) : base(settings) { }

    /// <inheritdoc />
    protected override void BeginInjection(IInjectionContext context)
    {
        _builder.Reset();
        _layout = context.KeyboardLayout;
    }

    /// <inheritdoc />
    protected override void EndInjection()
    {
        if (_builder.ShiftHeld) { InjectionInterop.SendScanCode(AppConstants.LeftShiftScanCode, keyUp: true); }
        _builder.Reset();
        _layout = IntPtr.Zero;
    }

    /// <inheritdoc />
    protected override bool CanRepresentStroke(KeyStroke stroke)
        => stroke.IsSpecial || InjectionInterop.TryResolveScanCode(stroke.Character, _layout, out _, out _);

    /// <inheritdoc />
    protected override StrokeResult SendStroke(KeyStroke stroke)
    {
        ushort scanCode = 0;
        bool requiresShift = false;
        if (!stroke.IsSpecial && !InjectionInterop.TryResolveScanCode(stroke.Character, _layout, out scanCode, out requiresShift))
        {
            return StrokeResult.Skipped;
        }
        IReadOnlyList<ScancodeKeyEvent> events = _builder.Build(stroke, scanCode, requiresShift);
        InjectionInterop.KeyEvent[] nativeEvents = events.Select(item => new InjectionInterop.KeyEvent(item.ScanCode, item.KeyUp)).ToArray();
        return InjectionInterop.SendScanCodes(nativeEvents) == (uint)events.Count ? StrokeResult.Sent(events.Count) : StrokeResult.Failure;
    }
}
