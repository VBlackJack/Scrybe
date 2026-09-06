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

using System.ComponentModel;
using System.Runtime.InteropServices;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;

namespace Scrybe.App.Services;

/// <summary>
/// WPF clipboard writer. Marshals the write onto the UI (STA) thread, which is required for
/// clipboard access. Transient "clipboard busy" exceptions propagate so the retry policy in the
/// clipboard service can handle them.
/// </summary>
public sealed class WpfClipboardWriter : IClipboardWriter
{
    /// <inheritdoc />
    public string? GetText()
        => System.Windows.Application.Current.Dispatcher.Invoke(
            () => System.Windows.Clipboard.ContainsText()
                ? System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText)
                : null);

    /// <inheritdoc />
    public void SetText(string text)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(
            () => System.Windows.Clipboard.SetText(text, System.Windows.TextDataFormat.UnicodeText));
    }
    /// <inheritdoc />
    public ClipboardSnapshot? GetSnapshot()
        => System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            uint before = GetClipboardSequenceNumber();
            string? text = GetText();
            uint after = GetClipboardSequenceNumber();
            if (before == 0 || before != after)
            {
                throw new InvalidOperationException("Clipboard changed while reading.");
            }
            return text is null ? null : new ClipboardSnapshot(text, after);
        });

    /// <inheritdoc />
    public bool TryClear(uint version)
        => System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (version == 0) { return false; }
            if (!OpenClipboard(IntPtr.Zero)) { throw new Win32Exception(Marshal.GetLastPInvokeError()); }
            try
            {
                // The open clipboard excludes competing writers during comparison and clearing.
                if (GetClipboardSequenceNumber() != version) { return false; }
                if (!EmptyClipboard()) { throw new Win32Exception(Marshal.GetLastPInvokeError()); }
                return true;
            }
            finally { CloseClipboard(); }
        });

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();
}
