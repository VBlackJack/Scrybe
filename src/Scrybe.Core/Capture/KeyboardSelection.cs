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

using Scrybe.Core.Models;

namespace Scrybe.Core.Capture;

/// <summary>Keyboard selection geometry in physical pixels, independent of pointer and display DPI.</summary>
public static class KeyboardSelection
{
    /// <summary>Creates a centered rectangle without requiring mouse input.</summary>
    /// <param name="width">Monitor pixel width.</param>
    /// <param name="height">Monitor pixel height.</param>
    public static PixelRect Create(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        int selectedWidth = Math.Max(1, width / AppConstants.KeyboardSelectionInitialDivisor);
        int selectedHeight = Math.Max(1, height / AppConstants.KeyboardSelectionInitialDivisor);
        return new PixelRect((width - selectedWidth) / 2, (height - selectedHeight) / 2, selectedWidth, selectedHeight);
    }

    /// <summary>Moves or resizes a rectangle while preserving monitor bounds and minimum size.</summary>
    /// <param name="selection">Current physical selection.</param>
    /// <param name="dx">Horizontal pixel delta.</param>
    /// <param name="dy">Vertical pixel delta.</param>
    /// <param name="resize">Whether arrows resize the right/bottom edge instead of moving.</param>
    /// <param name="width">Monitor width.</param>
    /// <param name="height">Monitor height.</param>
    public static PixelRect Adjust(PixelRect selection, int dx, int dy, bool resize, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(selection);
        int minimumWidth = Math.Min(AppConstants.MinSelectionPhysicalPixels, width - selection.X);
        int minimumHeight = Math.Min(AppConstants.MinSelectionPhysicalPixels, height - selection.Y);
        return resize
            ? selection with
            {
                Width = (int)Math.Clamp((long)selection.Width + dx, minimumWidth, width - selection.X),
                Height = (int)Math.Clamp((long)selection.Height + dy, minimumHeight, height - selection.Y),
            }
            : selection with
            {
                X = (int)Math.Clamp((long)selection.X + dx, 0, width - selection.Width),
                Y = (int)Math.Clamp((long)selection.Y + dy, 0, height - selection.Height),
            };
    }
}
