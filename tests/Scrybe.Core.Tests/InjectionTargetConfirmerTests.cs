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
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Regression tests for the password-grade target-confirmation gate.</summary>
public sealed class InjectionTargetConfirmerTests
{
    private static readonly IntPtr Target = new(0x1234);
    private const string ConfirmTitle = "Confirm injection";
    private const string ConfirmTemplate = "Type into {0} ({1}, pid {2}, hwnd {3})?";
    private const string TargetUnavailable = "Target unavailable";
    private const string Untitled = "Untitled";

    [Fact]
    public void TryConfirmAndRestore_CancelAtConfirmation_ReturnsFalseAndDoesNotRestore()
    {
        FakeTargetWindowGateway gateway = new();
        gateway.EnqueueInfo(CreateTargetInfo());
        FakeTargetConfirmationPrompt prompt = new()
        {
            ConfirmResult = false,
        };
        InjectionTargetConfirmer confirmer = new(gateway, prompt);

        bool result = confirmer.TryConfirmAndRestore(
            Target,
            ConfirmTitle,
            ConfirmTemplate,
            TargetUnavailable,
            Untitled);

        result.Should().BeFalse();
        gateway.RestoreCallCount.Should().Be(0);
        prompt.ConfirmCallCount.Should().Be(1);
        prompt.UnavailableCallCount.Should().Be(0);
    }

    [Fact]
    public void TryConfirmAndRestore_TargetInfoUnavailable_ReturnsFalse()
    {
        FakeTargetWindowGateway gateway = new();
        gateway.EnqueueUnavailable();
        FakeTargetConfirmationPrompt prompt = new();
        InjectionTargetConfirmer confirmer = new(gateway, prompt);

        bool result = confirmer.TryConfirmAndRestore(
            Target,
            ConfirmTitle,
            ConfirmTemplate,
            TargetUnavailable,
            Untitled);

        result.Should().BeFalse();
        gateway.RestoreCallCount.Should().Be(0);
        prompt.ConfirmCallCount.Should().Be(0);
        prompt.UnavailableCallCount.Should().Be(1);
    }

    [Fact]
    public void TryConfirmAndRestore_TargetVanishesAfterConfirmation_ReturnsFalseAndDoesNotRestore()
    {
        FakeTargetWindowGateway gateway = new();
        gateway.EnqueueInfo(CreateTargetInfo());
        gateway.EnqueueUnavailable();
        FakeTargetConfirmationPrompt prompt = new()
        {
            ConfirmResult = true,
        };
        InjectionTargetConfirmer confirmer = new(gateway, prompt);

        bool result = confirmer.TryConfirmAndRestore(
            Target,
            ConfirmTitle,
            ConfirmTemplate,
            TargetUnavailable,
            Untitled);

        result.Should().BeFalse();
        gateway.RestoreCallCount.Should().Be(0);
        prompt.ConfirmCallCount.Should().Be(1);
        prompt.UnavailableCallCount.Should().Be(1);
    }

    [Fact]
    public void TryConfirmAndRestore_TargetChangesAfterConfirmation_ReturnsFalseAndDoesNotRestore()
    {
        FakeTargetWindowGateway gateway = new();
        gateway.EnqueueInfo(CreateTargetInfo());
        gateway.EnqueueInfo(CreateTargetInfo(hwnd: new IntPtr(0x5678), processId: 84));
        FakeTargetConfirmationPrompt prompt = new()
        {
            ConfirmResult = true,
        };
        InjectionTargetConfirmer confirmer = new(gateway, prompt);

        bool result = confirmer.TryConfirmAndRestore(
            Target,
            ConfirmTitle,
            ConfirmTemplate,
            TargetUnavailable,
            Untitled);

        result.Should().BeFalse();
        gateway.RestoreCallCount.Should().Be(0);
        prompt.ConfirmCallCount.Should().Be(1);
        prompt.UnavailableCallCount.Should().Be(1);
    }

    [Fact]
    public void TryConfirmAndRestore_RestoreFails_ReturnsFalse()
    {
        FakeTargetWindowGateway gateway = new()
        {
            RestoreResult = false,
        };
        gateway.EnqueueInfo(CreateTargetInfo());
        gateway.EnqueueInfo(CreateTargetInfo());
        FakeTargetConfirmationPrompt prompt = new()
        {
            ConfirmResult = true,
        };
        InjectionTargetConfirmer confirmer = new(gateway, prompt);

        bool result = confirmer.TryConfirmAndRestore(
            Target,
            ConfirmTitle,
            ConfirmTemplate,
            TargetUnavailable,
            Untitled);

        result.Should().BeFalse();
        gateway.RestoreCallCount.Should().Be(1);
        prompt.ConfirmCallCount.Should().Be(1);
        prompt.UnavailableCallCount.Should().Be(1);
    }

    [Fact]
    public void TryConfirmAndRestore_RestoreAcceptedButForegroundDiffers_ReturnsFalse()
    {
        FakeTargetWindowGateway gateway = new()
        {
            RestoreResult = true,
            ForegroundWindow = new IntPtr(0x5678),
        };
        gateway.EnqueueInfo(CreateTargetInfo());
        gateway.EnqueueInfo(CreateTargetInfo());
        FakeTargetConfirmationPrompt prompt = new()
        {
            ConfirmResult = true,
        };
        InjectionTargetConfirmer confirmer = new(gateway, prompt);

        bool result = confirmer.TryConfirmAndRestore(
            Target,
            ConfirmTitle,
            ConfirmTemplate,
            TargetUnavailable,
            Untitled);

        result.Should().BeFalse();
        gateway.RestoreCallCount.Should().Be(1);
        prompt.ConfirmCallCount.Should().Be(1);
        prompt.UnavailableCallCount.Should().Be(1);
    }

    [Fact]
    public void TryConfirmAndRestore_ConfirmedValidAndRestoreOk_ReturnsTrue()
    {
        FakeTargetWindowGateway gateway = new()
        {
            RestoreResult = true,
        };
        gateway.EnqueueInfo(CreateTargetInfo());
        gateway.EnqueueInfo(CreateTargetInfo());
        FakeTargetConfirmationPrompt prompt = new()
        {
            ConfirmResult = true,
        };
        InjectionTargetConfirmer confirmer = new(gateway, prompt);

        bool result = confirmer.TryConfirmAndRestore(
            Target,
            ConfirmTitle,
            ConfirmTemplate,
            TargetUnavailable,
            Untitled);

        result.Should().BeTrue();
        gateway.RestoreCallCount.Should().Be(1);
        prompt.ConfirmCallCount.Should().Be(1);
        prompt.UnavailableCallCount.Should().Be(0);
        prompt.LastConfirmMessage.Should().Contain("Console");
        prompt.LastConfirmMessage.Should().Contain("0x1234");
    }

    private static TargetWindowInfo CreateTargetInfo(
        IntPtr? hwnd = null,
        string title = "Console",
        string processName = "WindowsTerminal",
        int processId = 42)
        => new(hwnd ?? Target, title, processName, processId);

    private sealed class FakeTargetWindowGateway : ITargetWindowGateway
    {
        private readonly Queue<TargetWindowInfo?> _infos = [];

        public bool RestoreResult { get; init; } = true;

        public int RestoreCallCount { get; private set; }

        public IntPtr ForegroundWindow { get; init; } = Target;

        public IntPtr GetForegroundWindow() => ForegroundWindow;

        public void EnqueueInfo(TargetWindowInfo info) => _infos.Enqueue(info);

        public void EnqueueUnavailable() => _infos.Enqueue(null);

        public bool TryGetInfo(IntPtr window, out TargetWindowInfo? target)
        {
            target = _infos.Count == 0 ? null : _infos.Dequeue();
            return target is not null;
        }

        public bool TryRestore(IntPtr window)
        {
            RestoreCallCount++;
            return RestoreResult;
        }
    }

    private sealed class FakeTargetConfirmationPrompt : ITargetConfirmationPrompt
    {
        public bool ConfirmResult { get; init; } = true;

        public int ConfirmCallCount { get; private set; }

        public int UnavailableCallCount { get; private set; }

        public string? LastConfirmMessage { get; private set; }

        public bool Confirm(string title, string message)
        {
            ConfirmCallCount++;
            LastConfirmMessage = message;
            return ConfirmResult;
        }

        public void ShowTargetUnavailable(string title, string message) => UnavailableCallCount++;
    }
}
