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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Scrybe.Core.Logging;

/// <summary>
/// Thread-safe, file-based application logger. Entries are queued and drained to a
/// daily log file by a background timer, and mirrored to the debugger output window.
/// The logger must never throw: all I/O failures are swallowed internally so that a
/// logging problem can never crash the application.
/// </summary>
public static class FileLogger
{
    /// <summary>Severity levels supported by the logger.</summary>
    private enum LogLevel
    {
        /// <summary>Lifecycle and informational events.</summary>
        Info,

        /// <summary>Recoverable issues such as fallbacks or retries.</summary>
        Warn,

        /// <summary>Failures and caught exceptions.</summary>
        Error,
    }

    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";
    private const string FileDateFormat = "yyyyMMdd";

    private static readonly ConcurrentQueue<string> Queue = new();
    private static readonly Lock WriteGate = new();

    private static Timer? _flushTimer;
    private static string? _logDirectory;
    private static volatile bool _isEnabled = true;

    /// <summary>
    /// Initializes the logger, creating <paramref name="logDirectory"/> if it does not exist
    /// and starting the background flush timer. Safe to call more than once.
    /// </summary>
    /// <param name="logDirectory">Directory in which daily log files are written.</param>
    public static void Initialize(string logDirectory)
    {
        try
        {
            _logDirectory = logDirectory;
            Directory.CreateDirectory(logDirectory);
        }
        catch
        {
            // Never throw from the logger: an unusable directory must not crash startup.
        }

        try
        {
            _flushTimer ??= new Timer(
                static _ => DrainQueue(),
                state: null,
                dueTime: AppConstants.LogFlushIntervalMs,
                period: AppConstants.LogFlushIntervalMs);
        }
        catch
        {
            // If the timer cannot be created, Flush() can still drain the queue manually.
        }
    }

    /// <summary>Enables or disables logging at runtime. When disabled, entries are discarded.</summary>
    /// <param name="enabled"><see langword="true"/> to record entries; otherwise <see langword="false"/>.</param>
    public static void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;

        if (!enabled)
        {
            lock (WriteGate)
            {
                while (Queue.TryDequeue(out _))
                {
                }
            }
        }
    }

    /// <summary>Records an informational entry.</summary>
    /// <param name="message">The message to record.</param>
    public static void Info(string message) => Enqueue(LogLevel.Info, message);

    /// <summary>Records a warning entry.</summary>
    /// <param name="message">The message to record.</param>
    public static void Warn(string message) => Enqueue(LogLevel.Warn, message);

    /// <summary>Records an error entry.</summary>
    /// <param name="message">The message to record.</param>
    public static void Error(string message) => Enqueue(LogLevel.Error, message);

    /// <summary>Records an error entry together with the details of an exception.</summary>
    /// <param name="message">The message to record.</param>
    /// <param name="exception">The exception whose details are appended to the entry.</param>
    public static void Error(string message, Exception exception)
        => Enqueue(LogLevel.Error, $"{message} | {exception.GetType().Name}: {exception.Message}");

    /// <summary>Forces an immediate, synchronous write of all queued entries.</summary>
    public static void Flush() => DrainQueue();

    /// <summary>Formats an entry, mirrors it to the debugger, and enqueues it for writing.</summary>
    private static void Enqueue(LogLevel level, string message)
    {
        if (!_isEnabled)
        {
            return;
        }

        try
        {
            string timestamp = DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture);
            string line = $"[{timestamp}] [{level.ToString().ToUpperInvariant()}] {message}";
            Debug.WriteLine(line);
            Queue.Enqueue(line);
        }
        catch
        {
            // Formatting an entry must never throw.
        }
    }

    /// <summary>Drains the queue to today's log file under a lock to serialize file access.</summary>
    private static void DrainQueue()
    {
        if (Queue.IsEmpty)
        {
            return;
        }

        lock (WriteGate)
        {
            try
            {
                string? directory = _logDirectory;
                if (string.IsNullOrEmpty(directory))
                {
                    return;
                }

                string fileName = $"{AppConstants.AppName}_{DateTime.Now.ToString(FileDateFormat, CultureInfo.InvariantCulture)}{AppConstants.LogFileExtension}";
                string filePath = Path.Combine(directory, fileName);

                StringBuilder builder = new();
                while (Queue.TryDequeue(out string? line))
                {
                    builder.AppendLine(line);
                }

                if (builder.Length > 0)
                {
                    File.AppendAllText(filePath, builder.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Swallow every I/O failure: logging must never surface an error to callers.
            }
        }
    }
}
