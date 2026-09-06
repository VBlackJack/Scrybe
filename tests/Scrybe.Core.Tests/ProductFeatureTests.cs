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

using System.Text;
using System.Text.Json;
using Scrybe.App.Services;
using Scrybe.App.ViewModels;
using Scrybe.Core.Input;
using Scrybe.Core.Interfaces;
using Scrybe.Core.IO;
using Scrybe.Core.Models;
using Scrybe.Core.Secrets;
using Scrybe.Core.Security;
using Scrybe.Core.Services;
using Scrybe.Core.Settings;
using Scrybe.Core.Snippets;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Exercises user feature boundaries against disposable real stores and synthetic input.</summary>
public sealed class ProductFeatureTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ScrybeFeatures", Guid.NewGuid().ToString("N"));
    public ProductFeatureTests() => Directory.CreateDirectory(_directory);
    private string StorePath(string name) => Path.Combine(_directory, name);
    private static Snippet Item(string id = "one", string name = "example", string text = "echo {{value}}")
        => new(id, name, "synthetic", text, [new("value", "Value", "hello")]);

    [Fact]
    public async Task CancelledReviewLeavesBothPreviousOutputsUntouched()
    {
        OcrTextStore store = new(); store.Set("previous");
        Clipboard clipboard = new();
        Assert.Null(await CapturePublication.PublishAsync("new", store, clipboard, _ => Task.FromResult<string?>(null)));
        Assert.Equal("previous", store.LastText);
        Assert.Equal(0, clipboard.Writes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CapturePublishesExactAcceptedWhitespace(bool review)
    {
        const string edited = "  corrected\t\n";
        OcrTextStore store = new();
        Clipboard clipboard = new();
        string expected = review ? edited : " raw\n";
        Assert.Equal(expected, await CapturePublication.PublishAsync(" raw\n", store, clipboard,
            review ? _ => Task.FromResult<string?>(edited) : null));
        Assert.Equal(expected, store.LastText);
        Assert.Equal(expected, clipboard.Text);
        Assert.Equal(1, clipboard.Writes);
    }

    [Fact]
    public void ProfilesMatchOnlyExactProcessAndRejectAmbiguity()
    {
        InjectionProfile profile = new("mstsc", InjectionMode.Scancode, 75, 300);
        AppSettings settings = new() { InjectionProfiles = [profile] };
        Assert.Same(profile, InjectionProfile.Resolve(settings, "MSTSC"));
        Assert.Equal(settings.InjectionMode, InjectionProfile.Resolve(settings, "mstsc-other").Mode);
        settings.InjectionProfiles.Add(profile with { ProcessName = "MSTSC" });
        Assert.Equal(settings.InjectionMode, InjectionProfile.Resolve(settings, "mstsc").Mode);
    }

    [Theory]
    [InlineData("app.exe", "30", "10")]
    [InlineData("app", "abc", "10")]
    [InlineData("app", "30", "-1")]
    [InlineData("app", "99999", "10")]
    public void InvalidProfileDraftCannotBecomeAValidSnapshot(string process, string delay, string extra)
    {
        InjectionProfileEditor editor = new() { ProcessName = process, KeyDelayMs = delay, EnterExtraDelayMs = extra };
        Assert.False(editor.Snapshot().IsValid);
    }

    [Fact]
    public async Task ProfileControlsBothStrategyAndPacingThroughProgressDecorator()
    {
        RecordingInjector unicode = new(); RecordingInjector scancode = new();
        InjectionCoordinator coordinator = new(unicode, scancode, new OcrTextStore(), new Notification(), new Locale(), new AppSettings());
        await coordinator.InjectTextAsync("A\n", new Context(new("mstsc", InjectionMode.Scancode, 75, 300)));
        Assert.Empty(unicode.Delays);
        Assert.Equal(new[] { AppConstants.InjectionStartDelayMs, 75, 375 }, scancode.Delays);
    }

    [Fact]
    public async Task BackupsRestoreExactBytesAndRejectStaleStoreSaves()
    {
        string path = StorePath(AppConstants.SettingsFileName);
        JsonSettingsStore store = new(path);
        Assert.True(await store.SaveAsync(new() { ReviewOcrBeforeCopy = true }));
        byte[] original = await File.ReadAllBytesAsync(path);
        Assert.True(await store.SaveAsync(new() { ReviewOcrBeforeCopy = false }));
        StoreBackups backups = new(path);
        StoreBackup version = Assert.Single(await backups.ListAsync());
        RestorePreview preview = await backups.PreviewAsync(version.Id);
        Assert.Equal(1, preview.EntryCount);
        await backups.RestoreAsync(preview);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.False(await store.SaveAsync(new()));
        Assert.True((await store.LoadAsync()).Settings.ReviewOcrBeforeCopy);
        Assert.Equal(2, (await backups.ListAsync()).Count);
    }

    [Fact]
    public async Task RestoreRejectsChangesSincePreviewWithoutReplacingCurrentData()
    {
        string path = StorePath(AppConstants.SettingsFileName);
        JsonSettingsStore store = new(path);
        await store.SaveAsync(new());
        await store.SaveAsync(new() { ReviewOcrBeforeCopy = true });
        StoreBackups backups = new(path);
        RestorePreview preview = await backups.PreviewAsync((await backups.ListAsync())[0].Id);
        await store.SaveAsync(new() { InjectionKeyDelayMs = 50 });
        byte[] current = await File.ReadAllBytesAsync(path);
        await Assert.ThrowsAsync<IOException>(() => backups.RestoreAsync(preview));
        Assert.Equal(current, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task BackupChecksumAndIdentifierAreValidated()
    {
        string path = StorePath(AppConstants.SettingsFileName);
        JsonSettingsStore store = new(path);
        await store.SaveAsync(new()); await store.SaveAsync(new() { ReviewOcrBeforeCopy = true });
        StoreBackups backups = new(path);
        StoreBackup version = (await backups.ListAsync())[0];
        string file = Path.Combine(path + ".versions", version.Id);
        string envelope = await File.ReadAllTextAsync(file);
        await File.WriteAllTextAsync(file, envelope.Replace("\"Version\": 1", "\"Version\": 99", StringComparison.Ordinal));
        await Assert.ThrowsAsync<InvalidDataException>(() => backups.PreviewAsync(version.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => backups.PreviewAsync("../settings.json"));
    }

    [Fact]
    public async Task VersionHistoryIsBounded()
    {
        string path = StorePath(AppConstants.SettingsFileName);
        JsonSettingsStore store = new(path);
        for (int index = 0; index < StoreBackups.RetainedVersions + 3; index++)
        { Assert.True(await store.SaveAsync(new() { InjectionKeyDelayMs = index + 5 })); }
        Assert.Equal(StoreBackups.RetainedVersions, (await new StoreBackups(path).ListAsync()).Count);
    }

    [Fact]
    public async Task UnchangedSavesDoNotEvictUsefulVersions()
    {
        string path = StorePath(AppConstants.SettingsFileName);
        JsonSettingsStore store = new(path);
        await store.SaveAsync(new());
        await store.SaveAsync(new() { ReviewOcrBeforeCopy = true });
        for (int index = 0; index <= StoreBackups.RetainedVersions; index++)
        { Assert.True(await store.SaveAsync(new() { ReviewOcrBeforeCopy = true })); }
        Assert.Single(await new StoreBackups(path).ListAsync());
    }

    [Fact]
    public async Task SecretRestorationKeepsCiphertextAndProvesSameUserDecryptability()
    {
        string path = StorePath(AppConstants.SecretsFileName);
        DpapiSecretProtector protector = new();
        SecretEntry secret = new("id", "synthetic", null, protector.Protect("synthetic-secret"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        JsonSecretStore store = new(path);
        await store.SaveAsync([secret]); await store.SaveAsync([]);
        StoreBackups backups = new(path);
        StoreBackup version = Assert.Single(await backups.ListAsync());
        string envelope = await File.ReadAllTextAsync(Path.Combine(path + ".versions", version.Id));
        Assert.DoesNotContain("synthetic-secret", envelope, StringComparison.Ordinal);
        await backups.RestoreAsync(await backups.PreviewAsync(version.Id));
        SecretEntry restored = Assert.Single(await new JsonSecretStore(path).LoadAsync());
        Assert.Equal(secret.ProtectedSecret, restored.ProtectedSecret);
        char[] plaintext = protector.UnprotectToChars(restored.ProtectedSecret);
        try { Assert.Equal("synthetic-secret", new string(plaintext)); }
        finally { SecretMemory.Clear(plaintext); }
    }

    [Fact]
    public void ExchangeRoundTripPreservesTemplateAndParameters()
    {
        Snippet input = Item();
        Snippet output = Assert.Single(SnippetExchange.Import(SnippetExchange.Export([input])));
        Assert.Equal(input.Template, output.Template);
        Assert.Equal(input.Parameters, output.Parameters);
    }

    [Theory]
    [InlineData("{\"Format\":\"Scrybe.Snippets\",\"Version\":2,\"Snippets\":[]}")]
    [InlineData("{\"Format\":\"Scrybe.Snippets\",\"Version\":1,\"Version\":1,\"Snippets\":[]}")]
    [InlineData("{\"Format\":\"Scrybe.Snippets\",\"Version\":1,\"Snippets\":[null]}")]
    public void ExchangeRejectsUnsupportedAndMalformedEnvelopes(string json)
        => Assert.Throws<InvalidDataException>(() => SnippetExchange.Import(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void ExchangeRejectsUnknownSecretFieldsAndDuplicateIds()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("{\"Format\":\"Scrybe.Snippets\",\"Version\":1,\"Snippets\":[],\"Secrets\":[]}");
        Assert.Throws<JsonException>(() => SnippetExchange.Import(bytes));
        Assert.Throws<InvalidDataException>(() => SnippetExchange.Export([Item(), Item()]));
        Assert.Throws<InvalidDataException>(() => SnippetExchange.Import(new byte[SnippetExchange.MaximumBytes + 1]));
    }

    [Theory]
    [InlineData(SnippetConflictPolicy.KeepExisting, 1, "old")]
    [InlineData(SnippetConflictPolicy.ReplaceExisting, 1, "new")]
    [InlineData(SnippetConflictPolicy.ImportCopies, 2, "old")]
    public void ExchangeConflictPolicyIsExplicit(SnippetConflictPolicy policy, int count, string firstText)
    {
        IReadOnlyList<Snippet> merged = SnippetExchange.Merge([Item(text: "old")], [Item(text: "new")], policy);
        Assert.Equal(count, merged.Count); Assert.Equal(firstText, merged[0].Template);
        Assert.Equal(merged.Count, merged.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public async Task ImportIsAtomicAndRejectsStalePreview()
    {
        string path = StorePath(AppConstants.SnippetsFileName);
        SnippetLibrary library = new(new JsonSnippetStore(path));
        await library.LoadAsync(); await library.SaveAsync(Item());
        byte[] snapshot = await library.ExportAsync();
        using (FileStream lease = new(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(await library.ImportAsync(snapshot, [Item("two", "second")], SnippetConflictPolicy.KeepExisting));
        }
        Assert.Single(library.Snippets);
        await library.SaveAsync(Item("three", "third"));
        Assert.False(await library.ImportAsync(snapshot, [Item("two", "second")], SnippetConflictPolicy.KeepExisting));
        byte[] fresh = await library.ExportAsync();
        Assert.True(await library.ImportAsync(fresh, [Item("two", "second")], SnippetConflictPolicy.KeepExisting));
        Assert.Equal(3, (await new JsonSnippetStore(path).LoadAsync()).Count);
    }

    private sealed class Clipboard : IClipboardService
    {
        public int Writes { get; private set; }
        public string? Text { get; private set; }
        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default) => Task.FromResult(Text);
        public Task SetTextAsync(string text, CancellationToken cancellationToken = default) { Writes++; Text = text; return Task.CompletedTask; }
    }
    private sealed class Context(InjectionProfile profile) : IInjectionContext
    {
        public bool IsCurrent => true;
        public IntPtr KeyboardLayout => new(1);
        public InjectionProfile? Profile => profile;
    }
    private sealed class Notification : INotificationService { public void Notify(string title, string message) { } }
    private sealed class Locale : ILocalizationManager
    {
        public string this[string key] => key;
        public string Current => "en";
        public event EventHandler? LocaleChanged { add { } remove { } }
        public Task LoadAsync(string code, CancellationToken token = default) => Task.CompletedTask;
    }
    private sealed class RecordingInjector() : KeystrokeInjectorBase(new AppSettings())
    {
        public List<int> Delays { get; } = [];
        protected override bool IsHigherIntegrity() => false;
        protected override void ReleaseModifiers() { }
        protected override bool CanRepresentStroke(KeyStroke stroke) => true;
        protected override StrokeResult SendStroke(KeyStroke stroke) => StrokeResult.Sent(1);
        protected override Task DelayAsync(int milliseconds, CancellationToken token) { Delays.Add(milliseconds); return Task.CompletedTask; }
    }
    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
