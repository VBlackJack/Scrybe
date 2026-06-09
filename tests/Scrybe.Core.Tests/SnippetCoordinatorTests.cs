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

using FluentAssertions;
using Scrybe.App.Services;
using Scrybe.Core.Input;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Regression tests for snippet target restore before injection.</summary>
public sealed class SnippetCoordinatorTests
{
    private static readonly IntPtr Target = new(0x5678);

    [Fact]
    public async Task InjectResolvedSnippetAsync_RestoreFails_DoesNotInjectAndNotifiesFailure()
    {
        FakeKeystrokeInjector injector = new();
        FakeTargetWindowGateway gateway = new()
        {
            RestoreResult = false,
        };
        TestNotificationService notification = new();
        SnippetCoordinator coordinator = CreateCoordinator(injector, gateway, notification);

        bool result = await coordinator.InjectResolvedSnippetAsync(Target, "echo ok");

        result.Should().BeFalse();
        gateway.RestoreCallCount.Should().Be(1);
        injector.SequenceInjectionCallCount.Should().Be(0);
        notification.Messages.Should().ContainSingle().Which.Should().Be("Snippet not typed");
    }

    [Fact]
    public async Task InjectResolvedSnippetAsync_RestoreOk_InvokesInjectionOnce()
    {
        FakeKeystrokeInjector injector = new();
        FakeTargetWindowGateway gateway = new()
        {
            RestoreResult = true,
        };
        TestNotificationService notification = new();
        SnippetCoordinator coordinator = CreateCoordinator(injector, gateway, notification);

        bool result = await coordinator.InjectResolvedSnippetAsync(Target, "echo ok");

        result.Should().BeTrue();
        gateway.RestoreCallCount.Should().Be(1);
        injector.SequenceInjectionCallCount.Should().Be(1);
    }

    private static SnippetCoordinator CreateCoordinator(
        FakeKeystrokeInjector injector,
        FakeTargetWindowGateway gateway,
        TestNotificationService notification)
    {
        InjectionCoordinator injection = new(
            injector,
            injector,
            new TestOcrTextStore(),
            notification,
            new TestLocalizationManager(),
            new AppSettings());

        return new SnippetCoordinator(
            new SnippetLibrary(new EmptySnippetStore()),
            injection,
            gateway,
            notification,
            new TestLocalizationManager());
    }

    private sealed class FakeTargetWindowGateway : ITargetWindowGateway
    {
        public bool RestoreResult { get; init; } = true;

        public int RestoreCallCount { get; private set; }

        public IntPtr GetForegroundWindow() => Target;

        public bool TryGetInfo(IntPtr window, out TargetWindowInfo? target)
        {
            target = new TargetWindowInfo(window, "Console", "WindowsTerminal", 42);
            return true;
        }

        public bool TryRestore(IntPtr window)
        {
            RestoreCallCount++;
            return RestoreResult;
        }
    }

    private sealed class FakeKeystrokeInjector : IKeystrokeInjector
    {
        public int SequenceInjectionCallCount { get; private set; }

        public Task<InjectionResult> InjectAsync(
            KeystrokeSequence sequence,
            CancellationToken cancellationToken = default)
        {
            SequenceInjectionCallCount++;
            return Task.FromResult(new InjectionResult(
                Success: true,
                KeystrokesSent: sequence.Strokes.Count,
                UipiBlocked: false,
                Aborted: false));
        }

        public Task<InjectionResult> InjectAsync(
            ReadOnlyMemory<char> text,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new InjectionResult(
                Success: true,
                KeystrokesSent: text.Length,
                UipiBlocked: false,
                Aborted: false));
    }

    private sealed class EmptySnippetStore : ISnippetStore
    {
        public Task<IReadOnlyList<Snippet>> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Snippet>>([]);

        public Task<bool> SaveAsync(IReadOnlyList<Snippet> snippets, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class TestNotificationService : INotificationService
    {
        public List<string> Messages { get; } = [];

        public void Notify(string title, string message) => Messages.Add(message);
    }

    private sealed class TestLocalizationManager : ILocalizationManager
    {
        public string this[string key] => key switch
        {
            "AppTitle" => "Scrybe",
            "Inject.Done" => "Typed {0} key events ({1} characters skipped)",
            "Palette.TargetUnavailable" => "Snippet not typed",
            _ => key,
        };

        public string Current => "en";

        public event EventHandler? LocaleChanged
        {
            add { }
            remove { }
        }

        public Task LoadAsync(string localeCode, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TestOcrTextStore : IOcrTextStore
    {
        public string? LastText { get; private set; }

        public void Set(string text) => LastText = text;
    }
}
