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
using System.Runtime.InteropServices;
using System.Text;
using Scrybe.Core.Models;

namespace Scrybe.App.Interop;

/// <summary>
/// The only Win32 surface for injection: sends scancode keystrokes via <c>SendInput</c> and reports
/// whether the focused window runs at a higher integrity level than this process (the UIPI condition
/// under which synthetic input is silently filtered).
/// </summary>
internal static class InjectionInterop
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventScanCode = 0x0008;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenIntegrityLevel = 25;
    private const int IntegrityUnknown = -1;

    private const uint MapVkToScanCode = 0;
    private const int VkKeyScanFailure = -1;
    private const int CtrlOrAltShiftStateMask = 0x06;
    private const int ShiftStateMask = 0x01;

    /// <summary>Resolved metadata for a captured injection target window.</summary>
    /// <param name="Hwnd">The captured window handle.</param>
    /// <param name="Title">The current window title, when available.</param>
    /// <param name="ProcessName">The owning process name.</param>
    /// <param name="ProcessId">The owning process id.</param>
    public sealed record TargetInfo(IntPtr Hwnd, string Title, string ProcessName, int ProcessId);

    /// <summary>
    /// Resolves a character to a hardware scancode and shift state against the active keyboard layout,
    /// so injection types correctly regardless of the local layout (e.g. AZERTY vs QWERTY). Characters
    /// that require AltGr (Ctrl+Alt) on the active layout are out of safe-ASCII scope and rejected.
    /// </summary>
    /// <param name="character">The character to resolve.</param>
    /// <param name="scanCode">The resolved scancode when successful.</param>
    /// <param name="requiresShift">Whether Shift must be held.</param>
    /// <returns><see langword="true"/> if the character maps to a simple (no-AltGr) key on the active layout.</returns>
    public static bool TryResolveScanCode(char character, out ushort scanCode, out bool requiresShift)
    {
        scanCode = 0;
        requiresShift = false;

        IntPtr layout = GetKeyboardLayout(0);
        short result = VkKeyScanEx(character, layout);
        if (result == VkKeyScanFailure)
        {
            return false;
        }

        byte virtualKey = (byte)(result & 0xFF);
        int shiftState = (result >> 8) & 0xFF;
        if ((shiftState & CtrlOrAltShiftStateMask) != 0)
        {
            return false;
        }

        uint mappedScanCode = MapVirtualKeyEx(virtualKey, MapVkToScanCode, layout);
        if (mappedScanCode == 0)
        {
            return false;
        }

        scanCode = (ushort)mappedScanCode;
        requiresShift = (shiftState & ShiftStateMask) != 0;
        return true;
    }

    // Virtual-key codes for the modifier keys, released before injection so a still-held hotkey
    // chord (e.g. Ctrl+Alt, which is AltGr on AZERTY) cannot combine with the typed characters.
    private static readonly ushort[] ModifierVirtualKeys = [0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5];

    /// <summary>Releases any held modifier keys (Shift/Ctrl/Alt) so injected characters are not altered by them.</summary>
    public static void ReleaseModifiers()
    {
        foreach (ushort virtualKey in ModifierVirtualKeys)
        {
            SendVirtualKey(virtualKey, keyUp: true);
        }
    }

    /// <summary>A single scancode key event for atomic batched injection.</summary>
    /// <param name="ScanCode">The set-1 scancode.</param>
    /// <param name="KeyUp"><see langword="true"/> for key release; otherwise key press.</param>
    public readonly record struct KeyEvent(ushort ScanCode, bool KeyUp);

    /// <summary>Sends a single scancode key event. Returns the number of events injected (0 on failure).</summary>
    /// <param name="scanCode">The set-1 scancode.</param>
    /// <param name="keyUp"><see langword="true"/> for key release; otherwise key press.</param>
    public static uint SendScanCode(ushort scanCode, bool keyUp)
        => SendScanCodes([new KeyEvent(scanCode, keyUp)]);

    /// <summary>
    /// Sends a batch of scancode key events in a single <c>SendInput</c> call so the events for one
    /// character (e.g. Shift-down, key-down, key-up, Shift-up) are injected atomically and adjacently,
    /// which prevents the dropped/duplicated keystrokes that separate calls can cause under load.
    /// </summary>
    /// <param name="events">The ordered key events to inject as one batch.</param>
    /// <returns>The number of events successfully injected.</returns>
    public static uint SendScanCodes(IReadOnlyList<KeyEvent> events)
    {
        Input[] inputs = new Input[events.Count];
        for (int i = 0; i < events.Count; i++)
        {
            inputs[i] = new Input
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = 0,
                        ScanCode = events[i].ScanCode,
                        Flags = KeyEventScanCode | (events[i].KeyUp ? KeyEventKeyUp : 0),
                        Time = 0,
                        ExtraInfo = IntPtr.Zero,
                    },
                },
            };
        }

        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    /// <summary>
    /// Sends a batch of Unicode-mode key events (code units via <c>KEYEVENTF_UNICODE</c>, control keys
    /// via virtual-key codes) in a single atomic <c>SendInput</c> call.
    /// </summary>
    /// <param name="events">The ordered events to inject as one batch.</param>
    /// <returns>The number of events successfully injected.</returns>
    public static uint SendUnicodeKeyEvents(IReadOnlyList<UnicodeKeyEvent> events)
    {
        Input[] inputs = new Input[events.Count];
        for (int i = 0; i < events.Count; i++)
        {
            UnicodeKeyEvent keyEvent = events[i];
            uint flags = (keyEvent.IsVirtualKey ? 0u : KeyEventUnicode) | (keyEvent.IsKeyUp ? KeyEventKeyUp : 0u);
            inputs[i] = new Input
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = keyEvent.IsVirtualKey ? keyEvent.Value : (ushort)0,
                        ScanCode = keyEvent.IsVirtualKey ? (ushort)0 : keyEvent.Value,
                        Flags = flags,
                        Time = 0,
                        ExtraInfo = IntPtr.Zero,
                    },
                },
            };
        }

        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static uint SendVirtualKey(ushort virtualKey, bool keyUp)
    {
        Input[] inputs =
        [
            new Input
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = virtualKey,
                        ScanCode = 0,
                        Flags = keyUp ? KeyEventKeyUp : 0,
                        Time = 0,
                        ExtraInfo = IntPtr.Zero,
                    },
                },
            },
        ];

        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    /// <summary>Returns the handle of the current foreground window (the intended injection target).</summary>
    public static IntPtr GetForegroundWindowHandle() => GetForegroundWindow();

    /// <summary>Resolves metadata for <paramref name="window"/> when the target is still alive.</summary>
    /// <param name="window">The captured target window.</param>
    /// <param name="target">The resolved metadata.</param>
    /// <returns><see langword="true"/> when the window and process can be resolved.</returns>
    public static bool TryGetTargetInfo(IntPtr window, out TargetInfo? target)
    {
        target = null;
        if (window == IntPtr.Zero || !IsWindow(window))
        {
            return false;
        }

        GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0)
        {
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            target = new TargetInfo(window, GetTitle(window), process.ProcessName, (int)processId);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Restores foreground to <paramref name="window"/> (the console that owned focus before the palette).</summary>
    /// <param name="window">The window handle to reactivate.</param>
    public static void RestoreForeground(IntPtr window)
    {
        TryRestoreForeground(window);
    }

    /// <summary>Attempts to restore foreground to a still-existing target window.</summary>
    /// <param name="window">The captured target window.</param>
    /// <returns><see langword="true"/> when the restore call was accepted.</returns>
    public static bool TryRestoreForeground(IntPtr window)
    {
        return window != IntPtr.Zero && IsWindow(window) && SetForegroundWindow(window);
    }

    /// <summary>Returns whether the foreground window's process runs at a higher integrity level than this process.</summary>
    public static bool IsForegroundHigherIntegrity()
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(foreground, out uint targetProcessId);
        int targetIntegrity = GetProcessIntegrityLevel(targetProcessId);
        int selfIntegrity = GetProcessIntegrityLevel((uint)Environment.ProcessId);

        return targetIntegrity != IntegrityUnknown
            && selfIntegrity != IntegrityUnknown
            && targetIntegrity > selfIntegrity;
    }

    private static int GetProcessIntegrityLevel(uint processId)
    {
        IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
        {
            return IntegrityUnknown;
        }

        try
        {
            if (!OpenProcessToken(process, TokenQuery, out IntPtr token))
            {
                return IntegrityUnknown;
            }

            try
            {
                GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out int requiredSize);
                if (requiredSize == 0)
                {
                    return IntegrityUnknown;
                }

                IntPtr buffer = Marshal.AllocHGlobal(requiredSize);
                try
                {
                    if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, requiredSize, out _))
                    {
                        return IntegrityUnknown;
                    }

                    IntPtr sid = Marshal.ReadIntPtr(buffer);
                    int subAuthorityCount = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                    IntPtr lastSubAuthority = GetSidSubAuthority(sid, (uint)(subAuthorityCount - 1));
                    return Marshal.ReadInt32(lastSubAuthority);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private static string GetTitle(IntPtr window)
    {
        int length = GetWindowTextLength(window);
        if (length <= 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new(length + 1);
        return GetWindowText(window, builder, builder.Capacity) > 0 ? builder.ToString() : string.Empty;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int sizeOfInput);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern short VkKeyScanEx(char character, IntPtr layout);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr layout);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, IntPtr tokenInformation, int tokenInformationLength, out int returnLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint subAuthorityIndex);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParamLow;
        public ushort ParamHigh;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }
}
