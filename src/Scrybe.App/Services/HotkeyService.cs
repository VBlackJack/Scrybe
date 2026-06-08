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

using System.Runtime.InteropServices;
using System.Windows.Interop;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;

namespace Scrybe.App.Services;

/// <summary>
/// Registers multiple system-wide hotkeys through the Win32 <c>RegisterHotKey</c> API, using a hidden
/// message-only window to receive <c>WM_HOTKEY</c> notifications on the UI thread. Each caller id is
/// mapped to a distinct native hotkey id.
/// </summary>
public sealed class HotkeyService : IHotkeyService
{
    private const int WmHotkey = 0x0312;
    private const int FirstNativeId = 0x4271;
    private const uint ModNoRepeat = 0x4000;
    private static readonly IntPtr MessageOnlyParent = new(-3);

    private readonly Dictionary<string, int> _nativeIdById = [];
    private readonly Dictionary<int, string> _idByNativeId = [];

    private HwndSource? _messageWindow;
    private int _nextNativeId = FirstNativeId;

    /// <inheritdoc />
    public event EventHandler<string>? HotkeyPressed;

    /// <inheritdoc />
    public bool TryRegister(string id, HotkeyDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(definition);

        EnsureMessageWindow();
        Unregister(id);

        int nativeId = _nextNativeId++;
        uint modifiers = (uint)definition.Modifiers | ModNoRepeat;
        bool success = RegisterHotKey(_messageWindow!.Handle, nativeId, modifiers, definition.VirtualKey);

        if (success)
        {
            _nativeIdById[id] = nativeId;
            _idByNativeId[nativeId] = id;
            FileLogger.Info($"Global hotkey '{id}' registered: {definition.DisplayName}.");
        }
        else
        {
            FileLogger.Warn($"Failed to register global hotkey '{id}': {definition.DisplayName} (it may be in use).");
        }

        return success;
    }

    /// <inheritdoc />
    public void Unregister(string id)
    {
        if (_messageWindow is null || !_nativeIdById.TryGetValue(id, out int nativeId))
        {
            return;
        }

        UnregisterHotKey(_messageWindow.Handle, nativeId);
        _nativeIdById.Remove(id);
        _idByNativeId.Remove(nativeId);
        FileLogger.Info($"Global hotkey '{id}' unregistered.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_messageWindow is null)
        {
            return;
        }

        foreach (int nativeId in _idByNativeId.Keys)
        {
            UnregisterHotKey(_messageWindow.Handle, nativeId);
        }

        _nativeIdById.Clear();
        _idByNativeId.Clear();

        _messageWindow.RemoveHook(WndProc);
        _messageWindow.Dispose();
        _messageWindow = null;
    }

    private void EnsureMessageWindow()
    {
        if (_messageWindow is not null)
        {
            return;
        }

        HwndSourceParameters parameters = new("ScrybeHotkeyWindow")
        {
            ParentWindow = MessageOnlyParent,
            WindowStyle = 0,
        };

        _messageWindow = new HwndSource(parameters);
        _messageWindow.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && _idByNativeId.TryGetValue(wParam.ToInt32(), out string? id))
        {
            HotkeyPressed?.Invoke(this, id);
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
