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

using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Scrybe.App.Interop;
using Scrybe.Core;
using Scrybe.Core.Capture;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace Scrybe.App.Views;

/// <summary>
/// Full-monitor, topmost overlay that renders a frozen capture and lets the user drag a selection
/// rectangle with a magnifier loupe and a live physical-pixel readout. The crop math is delegated
/// to the pure <see cref="SelectionGeometry"/>; this code-behind only handles view interaction.
/// </summary>
public sealed partial class CaptureOverlayWindow : Window
{
    private const double LoupeSizeDip = 150.0;
    private const double LoupeMagnification = 8.0;
    private const double LoupeOffsetDip = 24.0;
    private const double HintTopMarginDip = 40.0;
    private const double ReadoutOffsetDip = 8.0;

    private readonly BitmapSource _frozen;
    private readonly CapturedFrame _frame;
    private readonly double _scale;
    private readonly string _dimensionsFormat;

    private Point _startPoint;
    private Point _currentPoint;
    private bool _isDragging;
    private bool _completed;
    private PixelRect? _keyboardSelection;
    private readonly string _selectionDescription;

    /// <summary>Initializes the overlay for a captured frame.</summary>
    /// <param name="frozen">The frozen full-frame bitmap to display.</param>
    /// <param name="frame">The captured frame providing size and DPI.</param>
    /// <param name="localization">Source of localized overlay text.</param>
    public CaptureOverlayWindow(BitmapSource frozen, CapturedFrame frame, ILocalizationManager localization)
    {
        ArgumentNullException.ThrowIfNull(frozen);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(localization);

        InitializeComponent();

        _frozen = frozen;
        _frame = frame;
        _scale = frame.DpiScale;
        _dimensionsFormat = localization["Overlay.Dimensions"];

        FrozenImage.Source = frozen;
        LoupeBrush.ImageSource = frozen;
        HintText.Text = localization["Overlay.Hint"];
        _selectionDescription = localization["Overlay.KeyboardSelection"];
        AutomationProperties.SetName(this, localization["Overlay.Title"]);
        AutomationProperties.SetHelpText(this, HintText.Text);

        LoupeBorder.Width = LoupeSizeDip;
        LoupeBorder.Height = LoupeSizeDip;
        LoupeCenterMarker.Width = LoupeMagnification / _scale;
        LoupeCenterMarker.Height = LoupeMagnification / _scale;

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        KeyDown += OnKeyDown;
        Closed += OnClosed;
    }

    /// <summary>Raised once when the user confirms a selection (with a rectangle) or cancels (with <see langword="null"/>).</summary>
    public event EventHandler<PixelRect?>? SelectionCompleted;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Place the overlay in physical pixels once the HWND exists. WPF would otherwise convert
        // DIP coordinates using the primary monitor DPI, mislocating the window on other monitors
        // under mixed-DPI setups. After this move WPF adopts the target monitor DPI (Per-Monitor v2).
        WindowPlacementInterop.MoveToPhysicalBounds(
            this, _frame.MonitorLeft, _frame.MonitorTop, _frame.Width, _frame.Height);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();

        ScrimPath.Data = new RectangleGeometry(new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight));

        HintBorder.MaxWidth = RootGrid.ActualWidth;
        HintBorder.UpdateLayout();
        Canvas.SetLeft(HintBorder, Math.Max(0, (RootGrid.ActualWidth - HintBorder.ActualWidth) / 2.0));
        Canvas.SetTop(HintBorder, HintTopMarginDip);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _keyboardSelection = null;
        _startPoint = e.GetPosition(this);
        _currentPoint = _startPoint;
        _isDragging = true;
        HintBorder.Visibility = Visibility.Collapsed;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_keyboardSelection is not null) { return; }
        _currentPoint = e.GetPosition(this);
        UpdateLoupe(_currentPoint);

        if (_isDragging)
        {
            UpdateSelectionVisuals();
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ReleaseMouseCapture();
        Complete(BuildSelection());
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = HandleSelectionKey(e.Key, Keyboard.Modifiers);
    }

    /// <summary>Handles the same selection keys for routed input and controlled accessibility validation.</summary>
    public bool HandleSelectionKey(Key key, ModifierKeys modifiers)
    {
        if (key is Key.K or Key.Left or Key.Right or Key.Up or Key.Down)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            HintBorder.Visibility = Visibility.Visible;
            _keyboardSelection ??= KeyboardSelection.Create(_frame.Width, _frame.Height);
            int step = modifiers.HasFlag(ModifierKeys.Control) ? AppConstants.KeyboardSelectionFastStep : 1;
            int dx = key == Key.Left ? -step : key == Key.Right ? step : 0;
            int dy = key == Key.Up ? -step : key == Key.Down ? step : 0;
            _keyboardSelection = KeyboardSelection.Adjust(_keyboardSelection, dx, dy,
                modifiers.HasFlag(ModifierKeys.Shift), _frame.Width, _frame.Height);
            _startPoint = new Point(_keyboardSelection.X / _scale, _keyboardSelection.Y / _scale);
            _currentPoint = new Point((_keyboardSelection.X + _keyboardSelection.Width) / _scale,
                (_keyboardSelection.Y + _keyboardSelection.Height) / _scale);
            UpdateSelectionVisuals();
            UpdateLoupe(_currentPoint);
            string description = string.Format(CultureInfo.CurrentCulture, _selectionDescription,
                _keyboardSelection.X, _keyboardSelection.Y, _keyboardSelection.Width, _keyboardSelection.Height);
            ReadoutText.Text = description;
            PositionReadout(_startPoint.X, _startPoint.Y,
                _currentPoint.X - _startPoint.X, _currentPoint.Y - _startPoint.Y);
            AutomationProperties.SetName(ReadoutText, description);
            UIElementAutomationPeer.CreatePeerForElement(ReadoutText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            return true;
        }

        switch (key)
        {
            case Key.Escape:
                Complete(null);
                return true;
            case Key.Enter:
                Complete(_keyboardSelection ?? BuildSelection());
                return true;
            default:
                return false;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        FrozenImage.Source = null;
        LoupeBrush.ImageSource = null;
    }

    private PixelRect? BuildSelection() => SelectionGeometry.ToPhysicalCrop(
        _startPoint.X,
        _startPoint.Y,
        _currentPoint.X,
        _currentPoint.Y,
        _scale,
        _frame.Width,
        _frame.Height,
        AppConstants.MinSelectionPhysicalPixels);

    private void UpdateSelectionVisuals()
    {
        double left = Math.Min(_startPoint.X, _currentPoint.X);
        double top = Math.Min(_startPoint.Y, _currentPoint.Y);
        double width = Math.Abs(_currentPoint.X - _startPoint.X);
        double height = Math.Abs(_currentPoint.Y - _startPoint.Y);

        RectangleGeometry outer = new(new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight));
        RectangleGeometry inner = new(new Rect(left, top, width, height));
        ScrimPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);

        Canvas.SetLeft(SelectionRectangle, left);
        Canvas.SetTop(SelectionRectangle, top);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
        SelectionRectangle.Visibility = Visibility.Visible;

        int physicalWidth = (int)Math.Round(width * _scale);
        int physicalHeight = (int)Math.Round(height * _scale);
        ReadoutText.Text = string.Format(
            CultureInfo.InvariantCulture, _dimensionsFormat, physicalWidth, physicalHeight);
        ReadoutBorder.Visibility = Visibility.Visible;
        PositionReadout(left, top, width, height);
    }

    private void PositionReadout(double left, double top, double width, double height)
    {
        ReadoutBorder.UpdateLayout();

        double readoutX = left;
        double readoutY = top + height + ReadoutOffsetDip;

        if (readoutY + ReadoutBorder.ActualHeight > RootGrid.ActualHeight)
        {
            readoutY = top - ReadoutBorder.ActualHeight - ReadoutOffsetDip;
        }

        double maxX = RootGrid.ActualWidth - ReadoutBorder.ActualWidth;
        Canvas.SetLeft(ReadoutBorder, Math.Clamp(readoutX, 0, Math.Max(0, maxX)));
        Canvas.SetTop(ReadoutBorder, Math.Max(0, readoutY));
    }

    private void UpdateLoupe(Point cursor)
    {
        LoupeBorder.Visibility = Visibility.Visible;

        double cursorPhysicalX = cursor.X * _scale;
        double cursorPhysicalY = cursor.Y * _scale;
        double samplePhysical = LoupeSizeDip * _scale / LoupeMagnification;

        LoupeBrush.Viewbox = new Rect(
            (cursorPhysicalX - (samplePhysical / 2.0)) / _frame.Width,
            (cursorPhysicalY - (samplePhysical / 2.0)) / _frame.Height,
            samplePhysical / _frame.Width,
            samplePhysical / _frame.Height);

        double loupeX = cursor.X + LoupeOffsetDip;
        if (loupeX + LoupeSizeDip > RootGrid.ActualWidth)
        {
            loupeX = cursor.X - LoupeSizeDip - LoupeOffsetDip;
        }

        double loupeY = cursor.Y + LoupeOffsetDip;
        if (loupeY + LoupeSizeDip > RootGrid.ActualHeight)
        {
            loupeY = cursor.Y - LoupeSizeDip - LoupeOffsetDip;
        }

        Canvas.SetLeft(LoupeBorder, loupeX);
        Canvas.SetTop(LoupeBorder, loupeY);
    }

    private void Complete(PixelRect? result)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        SelectionCompleted?.Invoke(this, result);
        Close();
    }
}
