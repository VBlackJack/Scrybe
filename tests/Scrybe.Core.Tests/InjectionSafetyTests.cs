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

using Scrybe.App.Services;
using Scrybe.Core.Input;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Scrybe.Core.Tests.TestSupport;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Exercises paced typing with deterministic native-input substitutes.</summary>
public sealed class InjectionSafetyTests
{
    [Theory]
    [InlineData(true, 1, 0)]
    [InlineData(false, 1, 0)]
    [InlineData(true, 2, 1)]
    [InlineData(false, 2, 1)]
    public async Task TargetChangesDuringDelay_StopBeforeNextBatch(bool verbatim, int changeAtDelay, int expectedSent)
    {
        TestInjectionContext context = new();
        RecordingInjector injector = new() { OnDelay = call => { if (call == changeAtDelay) { context.IsCurrent = false; } } };
        InjectionResult result = verbatim
            ? await injector.InjectAsync("abc".AsMemory(), context: context)
            : await injector.InjectAsync(KeystrokeBuilder.Build("abc"), context: context);
        Assert.True(result.Aborted);
        Assert.Equal(expectedSent, injector.Sent.Count);
        Assert.Equal(1, injector.EndCount);
        Assert.Equal(2, injector.ReleaseCount);
    }

    [Fact]
    public async Task MissingContext_SendsNothingAndDoesNotReleaseUserKeys()
    {
        RecordingInjector injector = new();
        Assert.False((await injector.InjectAsync("abc".AsMemory())).Success);
        Assert.Empty(injector.Sent);
        Assert.Equal(0, injector.ReleaseCount);
    }

    [Theory]
    [InlineData(SpecialKey.Enter)]
    [InlineData(SpecialKey.Tab)]
    public async Task SpecialKeyFocusChange_StopsBeforeFollowingText(SpecialKey key)
    {
        TestInjectionContext context = new();
        RecordingInjector injector = new() { OnStroke = stroke => { if (stroke.Special == key) { context.IsCurrent = false; } } };
        string text = key == SpecialKey.Enter ? "A\nsecret" : "A\tsecret";
        Assert.True((await injector.InjectAsync(text.AsMemory(), context: context)).Aborted);
        Assert.Equal(2, injector.Sent.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NativeFailureOrException_AlwaysCleansUp(bool throws)
    {
        RecordingInjector injector = new() { Fail = !throws, Throw = throws };
        if (throws)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => injector.InjectAsync("x".AsMemory(), context: new TestInjectionContext()));
        }
        else
        {
            Assert.False((await injector.InjectAsync("x".AsMemory(), context: new TestInjectionContext())).Success);
        }
        Assert.Equal(1, injector.EndCount);
        Assert.Equal(2, injector.ReleaseCount);
    }

    [Fact]
    public async Task CancellationDuringSettle_ReleasesModifiers()
    {
        using CancellationTokenSource cancellation = new();
        RecordingInjector injector = new() { OnDelay = _ => cancellation.Cancel() };
        Assert.True((await injector.InjectAsync("x".AsMemory(), cancellation.Token, new TestInjectionContext())).Aborted);
        Assert.Empty(injector.Sent);
        Assert.Equal(1, injector.EndCount);
    }

    [Fact]
    public async Task PreflightAndSending_UseTheSameCapturedLayout()
    {
        TestInjectionContext context = new() { KeyboardLayout = new(0x409) };
        RecordingInjector injector = new();
        Assert.True((await injector.InjectAsync("Aa".AsMemory(), context: context)).Success);
        Assert.Equal(new IntPtr(0x409), injector.Layout);
        Assert.Equal(2, injector.PreflightCount);
        Assert.Equal(2, injector.Sent.Count);
    }

    [Fact]
    public void ConfirmedContext_RejectsForegroundIdentityAndLayoutChanges()
    {
        MutableGateway gateway = new();
        ConfirmedInjectionContext context = new(gateway, gateway.Info);
        Assert.True(context.IsCurrent);
        gateway.Foreground = new(2);
        Assert.False(context.IsCurrent);
        gateway.Foreground = new(1);
        Assert.False(context.IsCurrent); // An observed change remains invalidated.
        context = new(gateway, gateway.Info);
        gateway.Info = gateway.Info with { ProcessId = 22 };
        Assert.False(context.IsCurrent);
        context = new(gateway, gateway.Info);
        gateway.Layout = new(0x40c);
        Assert.False(context.IsCurrent);
    }

    private sealed class MutableGateway : ITargetWindowGateway
    {
        public TargetWindowInfo Info { get; set; } = new(new(1), "test", "test", 11);
        public IntPtr Foreground { get; set; } = new(1);
        public IntPtr Layout { get; set; } = new(0x409);
        public IntPtr GetForegroundWindow() => Foreground;
        public IntPtr GetKeyboardLayout(IntPtr window) => Layout;
        public bool TryGetInfo(IntPtr window, out TargetWindowInfo? target) { target = Info; return true; }
        public bool TryRestore(IntPtr window) => true;
    }

    private sealed class RecordingInjector : KeystrokeInjectorBase
    {
        public RecordingInjector() : base(new AppSettings()) { }
        public Action<int>? OnDelay { get; init; }
        public Action<KeyStroke>? OnStroke { get; init; }
        public bool Fail { get; init; }
        public bool Throw { get; init; }
        public int EndCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public int PreflightCount { get; private set; }
        public IntPtr Layout { get; private set; }
        public List<KeyStroke> Sent { get; } = [];
        private int _delayCount;
        protected override bool IsHigherIntegrity() => false;
        protected override void ReleaseModifiers() => ReleaseCount++;
        protected override void BeginInjection(IInjectionContext context) => Layout = context.KeyboardLayout;
        protected override void EndInjection() => EndCount++;
        protected override bool CanRepresentStroke(KeyStroke stroke) { PreflightCount++; return true; }
        protected override Task DelayAsync(int milliseconds, CancellationToken token)
        {
            OnDelay?.Invoke(++_delayCount);
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
        protected override StrokeResult SendStroke(KeyStroke stroke)
        {
            if (Throw) { throw new InvalidOperationException("Synthetic native failure"); }
            if (Fail) { return StrokeResult.Failure; }
            Sent.Add(stroke);
            OnStroke?.Invoke(stroke);
            return StrokeResult.Sent(1);
        }
    }
}
