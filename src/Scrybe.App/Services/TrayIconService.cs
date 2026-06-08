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

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Scrybe.Core;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Logging;
using Scrybe.Core.Models;
using MediaBrush = System.Windows.Media.SolidColorBrush;
using WpfApplication = System.Windows.Application;

namespace Scrybe.App.Services;

/// <summary>
/// Owns the system-tray <see cref="NotifyIcon"/>: a generated Dracula-accent icon plus a localized
/// context menu and a balloon used for brief confirmations. It raises
/// <see cref="CaptureRequested"/> instead of depending on the capture coordinator, which keeps the
/// dependency graph acyclic.
/// </summary>
public sealed class TrayIconService : IDisposable, INotificationService
{
    private const int IconSize = 32;
    private const float IconGlyphFontSize = 18f;
    private const int BalloonTimeoutMs = 3000;
    private const string AccentBrushKey = "AccentBrush";
    private const string GlyphFontFamily = "Segoe UI";

    private static readonly OcrCleanupMode[] CleanupModes =
        [OcrCleanupMode.Raw, OcrCleanupMode.Standard, OcrCleanupMode.LogCleaner, OcrCleanupMode.CodeFormatter];

    private readonly ILocalizationManager _localization;
    private readonly AppSettings _settings;
    private readonly List<ToolStripMenuItem> _cleanupModeItems = [];

    private NotifyIcon? _notifyIcon;
    private Icon? _icon;

    /// <summary>Initializes the tray service with the localization source and settings.</summary>
    /// <param name="localization">Source of localized menu labels and tooltip.</param>
    /// <param name="settings">Application settings providing the active cleanup mode.</param>
    public TrayIconService(ILocalizationManager localization, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(settings);
        _localization = localization;
        _settings = settings;
    }

    /// <summary>Raised when the user requests a capture from the tray (menu item or double-click).</summary>
    public event EventHandler? CaptureRequested;

    /// <summary>Raised when the user requests the main hub from the tray.</summary>
    public event EventHandler? ShowHubRequested;

    /// <summary>Raised when the user opens About from the tray.</summary>
    public event EventHandler? ShowAboutRequested;

    /// <summary>Raised when the user opens snippet management from the tray.</summary>
    public event EventHandler? ManageSnippetsRequested;

    /// <summary>Raised when the user opens secret management from the tray.</summary>
    public event EventHandler? ManageSecretsRequested;

    /// <summary>Raised when the user opens capture history from the tray.</summary>
    public event EventHandler? ShowHistoryRequested;

    /// <summary>Raised when the user selects a cleanup mode from the tray submenu.</summary>
    public event EventHandler<OcrCleanupMode>? CleanupModeChanged;

    /// <summary>Raised when the user opens the settings window from the tray.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Creates and shows the tray icon and its context menu.</summary>
    public void Initialize()
    {
        _icon = CreateTrayIcon();

        ContextMenuStrip menu = new();

        ToolStripMenuItem openItem = new(_localization["Tray.Open"]);
        openItem.Click += OnShowHubRequested;

        ToolStripMenuItem captureItem = new(_localization["Tray.Capture"]);
        captureItem.Click += OnCaptureRequested;

        ToolStripMenuItem historyItem = new(_localization["Tray.History"]);
        historyItem.Click += OnShowHistoryRequested;

        ToolStripMenuItem snippetsItem = new(_localization["Tray.ManageSnippets"]);
        snippetsItem.Click += OnManageSnippetsRequested;

        ToolStripMenuItem secretsItem = new(_localization["Tray.ManageSecrets"]);
        secretsItem.Click += OnManageSecretsRequested;

        ToolStripMenuItem settingsItem = new(_localization["Tray.Settings"]);
        settingsItem.Click += OnSettingsRequested;

        ToolStripMenuItem aboutItem = new(_localization["Tray.About"]);
        aboutItem.Click += OnShowAboutRequested;

        ToolStripMenuItem modeMenu = BuildCleanupModeMenu();

        ToolStripMenuItem quitItem = new(_localization["Tray.Quit"]);
        quitItem.Click += OnQuitRequested;

        menu.Items.Add(openItem);
        menu.Items.Add(captureItem);
        menu.Items.Add(historyItem);
        menu.Items.Add(modeMenu);
        menu.Items.Add(snippetsItem);
        menu.Items.Add(secretsItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(aboutItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = _localization["Tray.Tooltip"],
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += OnShowHubRequested;

        FileLogger.Info("Tray icon initialized.");
    }

    /// <inheritdoc />
    public void Notify(string title, string message)
    {
        _notifyIcon?.ShowBalloonTip(BalloonTimeoutMs, title, message, ToolTipIcon.Info);
    }

    /// <summary>Synchronizes the checked cleanup mode in the tray menu after another surface changed it.</summary>
    /// <param name="mode">The active cleanup mode.</param>
    public void UpdateCleanupMode(OcrCleanupMode mode)
    {
        foreach (ToolStripMenuItem item in _cleanupModeItems)
        {
            if (item.Tag is OcrCleanupMode itemMode)
            {
                item.Checked = itemMode == mode;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _icon?.Dispose();
        _icon = null;
    }

    private void OnCaptureRequested(object? sender, EventArgs e) => CaptureRequested?.Invoke(this, EventArgs.Empty);

    private void OnShowHubRequested(object? sender, EventArgs e) => ShowHubRequested?.Invoke(this, EventArgs.Empty);

    private void OnShowAboutRequested(object? sender, EventArgs e) => ShowAboutRequested?.Invoke(this, EventArgs.Empty);

    private void OnManageSnippetsRequested(object? sender, EventArgs e) => ManageSnippetsRequested?.Invoke(this, EventArgs.Empty);

    private void OnManageSecretsRequested(object? sender, EventArgs e) => ManageSecretsRequested?.Invoke(this, EventArgs.Empty);

    private void OnShowHistoryRequested(object? sender, EventArgs e) => ShowHistoryRequested?.Invoke(this, EventArgs.Empty);

    private void OnSettingsRequested(object? sender, EventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private ToolStripMenuItem BuildCleanupModeMenu()
    {
        ToolStripMenuItem modeMenu = new(_localization["Tray.CleanupMode"]);
        _cleanupModeItems.Clear();
        foreach (OcrCleanupMode mode in CleanupModes)
        {
            ToolStripMenuItem item = new(_localization[CleanupModeLabelKey(mode)])
            {
                Tag = mode,
                Checked = mode == _settings.CleanupMode,
            };
            item.Click += OnCleanupModeClicked;
            modeMenu.DropDownItems.Add(item);
            _cleanupModeItems.Add(item);
        }

        return modeMenu;
    }

    private void OnCleanupModeClicked(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem clicked || clicked.Tag is not OcrCleanupMode mode || clicked.Owner is null)
        {
            return;
        }

        foreach (ToolStripItem sibling in clicked.Owner.Items)
        {
            if (sibling is ToolStripMenuItem menuItem && menuItem.Tag is OcrCleanupMode siblingMode)
            {
                menuItem.Checked = siblingMode == mode;
            }
        }

        CleanupModeChanged?.Invoke(this, mode);
    }

    private static string CleanupModeLabelKey(OcrCleanupMode mode) => mode switch
    {
        OcrCleanupMode.Raw => "Tray.ModeRaw",
        OcrCleanupMode.Standard => "Tray.ModeStandard",
        OcrCleanupMode.LogCleaner => "Tray.ModeLogCleaner",
        OcrCleanupMode.CodeFormatter => "Tray.ModeCodeFormatter",
        _ => "Tray.ModeStandard",
    };

    private void OnQuitRequested(object? sender, EventArgs e)
    {
        FileLogger.Info("Quit requested from tray.");
        if (WpfApplication.Current is Scrybe.App.App app)
        {
            app.RequestShutdown();
            return;
        }

        WpfApplication.Current.Shutdown();
    }

    private static Icon CreateTrayIcon()
    {
        Color accent = ResolveAccentColor();
        string glyph = AppConstants.AppName[..1];

        using Bitmap bitmap = new(IconSize, IconSize);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);

            using SolidBrush backgroundBrush = new(accent);
            graphics.FillEllipse(backgroundBrush, 0, 0, IconSize - 1, IconSize - 1);

            using Font font = new(GlyphFontFamily, IconGlyphFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using SolidBrush foregroundBrush = new(Color.White);
            using StringFormat format = new()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            graphics.DrawString(glyph, font, foregroundBrush, new RectangleF(0, 0, IconSize, IconSize), format);
        }

        IntPtr iconHandle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(iconHandle).Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private static Color ResolveAccentColor()
    {
        if (WpfApplication.Current?.Resources[AccentBrushKey] is MediaBrush brush)
        {
            System.Windows.Media.Color color = brush.Color;
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        return Color.MediumPurple;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
