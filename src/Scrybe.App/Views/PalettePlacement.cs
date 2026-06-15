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

using System.Windows;
using WpfPoint = System.Windows.Point;
using WinFormsCursor = System.Windows.Forms.Cursor;
using WinFormsScreen = System.Windows.Forms.Screen;

namespace Scrybe.App.Views;

/// <summary>Positions transient palette windows near the user's current pointer.</summary>
internal static class PalettePlacement
{
    private const double OffsetDip = 24.0;

    /// <summary>Moves the palette near the cursor while keeping it inside the active work area.</summary>
    /// <param name="window">Palette window to move after layout has completed.</param>
    public static void PositionNearCursor(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        System.Drawing.Point cursor = WinFormsCursor.Position;
        WinFormsScreen screen = WinFormsScreen.FromPoint(cursor);

        WpfPoint cursorDip = window.PointFromScreen(new WpfPoint(cursor.X, cursor.Y));
        WpfPoint workTopLeft = window.PointFromScreen(new WpfPoint(screen.WorkingArea.Left, screen.WorkingArea.Top));
        WpfPoint workBottomRight = window.PointFromScreen(new WpfPoint(screen.WorkingArea.Right, screen.WorkingArea.Bottom));

        double width = ActualOrConfigured(window.ActualWidth, window.Width, window.MinWidth);
        double height = ActualOrConfigured(window.ActualHeight, window.Height, window.MinHeight);

        double left = cursorDip.X + OffsetDip;
        if (left + width > workBottomRight.X)
        {
            left = cursorDip.X - width - OffsetDip;
        }

        double top = cursorDip.Y + OffsetDip;
        if (top + height > workBottomRight.Y)
        {
            top = cursorDip.Y - height - OffsetDip;
        }

        window.Left = ClampToRange(left, workTopLeft.X, workBottomRight.X - width);
        window.Top = ClampToRange(top, workTopLeft.Y, workBottomRight.Y - height);
    }

    private static double ActualOrConfigured(double actual, double configured, double fallback)
    {
        if (actual > 0)
        {
            return actual;
        }

        return !double.IsNaN(configured) && configured > 0 ? configured : fallback;
    }

    private static double ClampToRange(double value, double min, double max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Clamp(value, min, max);
    }
}
