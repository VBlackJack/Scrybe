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

/// <summary>Regression tests for clipboard and secret injection outcome propagation.</summary>
public sealed class ClipboardInjectionCoordinatorTests
{
    private const string ClipboardText = "production-password";

    public static TheoryData<InjectionResult, string> FailureResults { get; } = new()
    {
        { new InjectionResult(Success: false, KeystrokesSent: 0, UipiBlocked: true, Aborted: false), "UIPI blocked" },
        { new InjectionResult(Success: false, KeystrokesSent: 4, UipiBlocked: false, Aborted: true), "Typing aborted" },
        { new InjectionResult(Success: false, KeystrokesSent: 2, UipiBlocked: false, Aborted: false), "Typing failed" },
    };

    [Theory]
    [MemberData(nameof(FailureResults))]
    public async Task InjectClipboardAsync_FailureKeepsClipboardAndSuppressesClipboardSuccess(
        InjectionResult injectionResult,
        string expectedMessage)
    {
        TestClipboardService clipboard = new(ClipboardText);
        TestNotificationService notification = new();
        ClipboardInjectionCoordinator coordinator = CreateClipboardCoordinator(
            injectionResult,
            clipboard,
            notification,
            clearClipboardAfterInjection: true);

        await coordinator.InjectClipboardAsync();

        clipboard.Text.Should().Be(ClipboardText);
        clipboard.SetCallCount.Should().Be(0);
        notification.Messages.Should().ContainSingle();
        notification.Messages[0].Should().Be(expectedMessage);
        notification.Messages.Should().NotContain("Clipboard text injected");
    }

    [Theory]
    [InlineData(true, "")]
    [InlineData(false, ClipboardText)]
    public async Task InjectClipboardAsync_SuccessClearsOnlyWhenConfiguredAndReportsOnce(
        bool clearClipboardAfterInjection,
        string expectedClipboardText)
    {
        TestClipboardService clipboard = new(ClipboardText);
        TestNotificationService notification = new();
        ClipboardInjectionCoordinator coordinator = CreateClipboardCoordinator(
            new InjectionResult(Success: true, KeystrokesSent: 10, UipiBlocked: false, Aborted: false),
            clipboard,
            notification,
            clearClipboardAfterInjection);

        await coordinator.InjectClipboardAsync();

        clipboard.Text.Should().Be(expectedClipboardText);
        clipboard.SetCallCount.Should().Be(clearClipboardAfterInjection ? 1 : 0);
        notification.Messages.Should().Equal("Clipboard text injected");
    }

    [Fact]
    public async Task InjectSecretAsync_ExceptionPathReturnsFailure()
    {
        FakeKeystrokeInjector injector = new(new InjectionResult(Success: true, KeystrokesSent: 1, UipiBlocked: false, Aborted: false))
        {
            ExceptionToThrow = new InvalidOperationException("fake failure"),
        };
        TestNotificationService notification = new();
        InjectionCoordinator coordinator = CreateInjectionCoordinator(injector, notification);

        InjectionResult result = await coordinator.InjectSecretAsync(ClipboardText.ToCharArray());

        result.Success.Should().BeFalse();
        result.UipiBlocked.Should().BeFalse();
        result.Aborted.Should().BeFalse();
        notification.Messages.Should().Equal("Typing failed");
    }

    [Fact]
    public async Task InjectSecretAsync_AlreadyInProgressReturnsFailure()
    {
        TaskCompletionSource<object?> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<object?> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeKeystrokeInjector injector = new(new InjectionResult(Success: true, KeystrokesSent: 1, UipiBlocked: false, Aborted: false))
        {
            Started = started,
            Release = release,
        };
        TestNotificationService notification = new();
        InjectionCoordinator coordinator = CreateInjectionCoordinator(injector, notification);

        Task<InjectionResult> first = coordinator.InjectSecretAsync("first".ToCharArray());
        await started.Task;

        InjectionResult second = await coordinator.InjectSecretAsync("second".ToCharArray());

        second.Success.Should().BeFalse();
        notification.Messages.Should().Equal("Typing failed");

        release.SetResult(null);
        InjectionResult firstResult = await first;
        firstResult.Success.Should().BeTrue();
    }

    [Fact]
    public async Task VerbatimInjection_UnrepresentableCharacterFailsBeforeTyping()
    {
        GuardedTestInjector injector = new();

        InjectionResult result = await injector.InjectAsync("déjà-secret".AsMemory());

        result.Success.Should().BeFalse();
        result.KeystrokesSent.Should().Be(0);
        injector.SentCharacters.Should().BeEmpty();
    }

    private static ClipboardInjectionCoordinator CreateClipboardCoordinator(
        InjectionResult injectionResult,
        TestClipboardService clipboard,
        TestNotificationService notification,
        bool clearClipboardAfterInjection)
    {
        FakeKeystrokeInjector injector = new(injectionResult);
        InjectionCoordinator injection = CreateInjectionCoordinator(injector, notification);
        AppSettings settings = new()
        {
            ClearClipboardAfterInjection = clearClipboardAfterInjection,
        };

        return new ClipboardInjectionCoordinator(
            clipboard,
            injection,
            new AllowingTargetConfirmer(),
            notification,
            new TestLocalizationManager(),
            settings);
    }

    private static InjectionCoordinator CreateInjectionCoordinator(
        FakeKeystrokeInjector injector,
        TestNotificationService notification)
    {
        return new InjectionCoordinator(
            injector,
            injector,
            new TestOcrTextStore(),
            notification,
            new TestLocalizationManager(),
            new AppSettings());
    }

    private sealed class FakeKeystrokeInjector : IKeystrokeInjector
    {
        private readonly InjectionResult _result;

        public FakeKeystrokeInjector(InjectionResult result) => _result = result;

        public Exception? ExceptionToThrow { get; init; }

        public TaskCompletionSource<object?>? Started { get; init; }

        public TaskCompletionSource<object?>? Release { get; init; }

        public Task<InjectionResult> InjectAsync(
            KeystrokeSequence sequence,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_result);

        public async Task<InjectionResult> InjectAsync(
            ReadOnlyMemory<char> text,
            CancellationToken cancellationToken = default)
        {
            Started?.TrySetResult(null);

            TaskCompletionSource<object?>? release = Release;
            if (release is not null)
            {
                await release.Task.ConfigureAwait(false);
            }

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return _result;
        }
    }

    private sealed class GuardedTestInjector : KeystrokeInjectorBase
    {
        public List<char> SentCharacters { get; } = [];

        public GuardedTestInjector()
            : base(new AppSettings())
        {
        }

        protected override bool CanRepresentStroke(KeyStroke stroke)
            => stroke.IsSpecial || stroke.Character < 0x80;

        protected override StrokeResult SendStroke(KeyStroke stroke)
        {
            if (!stroke.IsSpecial)
            {
                SentCharacters.Add(stroke.Character);
            }

            return StrokeResult.Sent(1);
        }
    }

    private sealed class TestClipboardService : IClipboardService
    {
        public TestClipboardService(string? text) => Text = text;

        public string? Text { get; private set; }

        public int SetCallCount { get; private set; }

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Text);

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            SetCallCount++;
            Text = text;
            return Task.CompletedTask;
        }
    }

    private sealed class TestNotificationService : INotificationService
    {
        public List<string> Messages { get; } = [];

        public void Notify(string title, string message) => Messages.Add(message);
    }

    private sealed class AllowingTargetConfirmer : IInjectionTargetConfirmer
    {
        public bool TryConfirmAndRestore(
            IntPtr target,
            string confirmTitle,
            string confirmMessageTemplate,
            string targetUnavailableMessage,
            string untitledTargetText)
            => true;
    }

    private sealed class TestLocalizationManager : ILocalizationManager
    {
        public string this[string key] => key switch
        {
            "AppTitle" => "Scrybe",
            "Inject.Aborted" => "Typing aborted",
            "Inject.ConfirmClipboardTarget" => "Confirm {0}",
            "Inject.ConfirmTitle" => "Confirm injection",
            "Inject.Done" => "Typed {0} key events ({1} characters skipped)",
            "Inject.Failed" => "Typing failed",
            "Inject.TargetUnavailable" => "Target unavailable",
            "Inject.UntitledTarget" => "Untitled",
            "Inject.UipiBlocked" => "UIPI blocked",
            "Notify.ClipboardEmpty" => "Clipboard empty",
            "Notify.ClipboardInjected" => "Clipboard text injected",
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
