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
using System.Windows.Interop;
using Scrybe.App.Interop;

namespace Scrybe.App.Theming;

/// <summary>Attached window theming helpers applied from resource dictionaries.</summary>
public static class WindowTheming
{
    private static bool _darkTitleBarHandlerRegistered;

    /// <summary>Attached property that enables the native dark title bar on a WPF window.</summary>
    public static readonly DependencyProperty UseDarkTitleBarProperty = DependencyProperty.RegisterAttached(
        "UseDarkTitleBar",
        typeof(bool),
        typeof(WindowTheming),
        new PropertyMetadata(false, OnUseDarkTitleBarChanged));

    /// <summary>Reads whether a window should use a dark native title bar.</summary>
    /// <param name="element">The target dependency object.</param>
    public static bool GetUseDarkTitleBar(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(UseDarkTitleBarProperty);
    }

    /// <summary>Sets whether a window should use a dark native title bar.</summary>
    /// <param name="element">The target dependency object.</param>
    /// <param name="value">Whether to apply the dark title bar.</param>
    public static void SetUseDarkTitleBar(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(UseDarkTitleBarProperty, value);
    }

    /// <summary>Applies dark title bars to every WPF window loaded by the app.</summary>
    public static void RegisterDarkTitleBarForWindows()
    {
        if (_darkTitleBarHandlerRegistered)
        {
            return;
        }

        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));

        _darkTitleBarHandlerRegistered = true;
    }

    private static void OnUseDarkTitleBarChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not Window window || args.NewValue is not true)
        {
            return;
        }

        ApplyWhenReady(window);
    }

    private static void ApplyWhenReady(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            WindowChromeInterop.ApplyDarkTitleBar(hwnd);
            return;
        }

        window.SourceInitialized -= OnWindowSourceInitialized;
        window.SourceInitialized += OnWindowSourceInitialized;
    }

    private static void OnWindowSourceInitialized(object? sender, EventArgs args)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.SourceInitialized -= OnWindowSourceInitialized;
        if (GetUseDarkTitleBar(window))
        {
            WindowChromeInterop.ApplyDarkTitleBar(new WindowInteropHelper(window).Handle);
        }
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is not Window window)
        {
            return;
        }

        SetUseDarkTitleBar(window, true);
        ApplyWhenReady(window);
    }
}
