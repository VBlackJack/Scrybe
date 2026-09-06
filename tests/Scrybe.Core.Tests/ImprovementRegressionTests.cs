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
using Scrybe.App.ViewModels;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Scrybe.Core.Secrets;
using Scrybe.Core.Security;
using Scrybe.Core.Services;
using Scrybe.Core.Snippets;
using Scrybe.Core.Text;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Exercises recovery against actual revisions and non-sensitive injection progress.</summary>
public sealed class ImprovementRegressionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ScrybeImprovements", Guid.NewGuid().ToString("N"));
    public ImprovementRegressionTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task SecretRecovery_PreservesDraftAndOtherWritersEntry_WithoutAutomaticSave()
    {
        string path = Path.Combine(_directory, "secrets.json");
        SecretLibrary library = new(new JsonSecretStore(path), new DpapiSecretProtector());
        await library.LoadAsync();
        SecretManagerViewModel model = new(library, new Locale(), new Confirmation());
        model.NewCommand.Execute(null);
        model.Name = "draft";
        model.SetSecretValue("synthetic-draft");
        SecretLibrary competing = new(new JsonSecretStore(path), new DpapiSecretProtector());
        await competing.LoadAsync();
        Assert.True((await competing.SaveAsync(null, "other", null, "synthetic-other")).Persisted);
        await model.SaveCommand.ExecuteAsync(null);
        Assert.True(model.IsStatusError);
        Assert.Equal("synthetic-draft", model.SecretValue);
        byte[] original = await File.ReadAllBytesAsync(path);
        await model.ReloadFromDiskCommand.ExecuteAsync(null);
        Assert.False(model.IsStatusError);
        Assert.True(model.HasPendingChanges);
        Assert.Null(model.SelectedSecret);
        Assert.Equal("draft", model.Name);
        Assert.Equal("synthetic-draft", model.SecretValue);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        await model.SaveCommand.ExecuteAsync(null);
        Assert.False(model.IsStatusError);
        Assert.Equal(2, (await new JsonSecretStore(path).LoadAsync()).Count);
    }

    [Fact]
    public async Task SnippetRecovery_KeepsParametersAndRejectsFailedReload()
    {
        string path = Path.Combine(_directory, "snippets.json");
        JsonSnippetStore store = new(path);
        Assert.True(await store.SaveAsync([new("one", "original", null, "echo hi", [])]));
        SnippetLibrary library = new(store);
        await library.LoadAsync();
        SnippetManagerViewModel model = new(library, new Locale(), new Confirmation());
        model.Reload();
        model.Name = "draft";
        model.Template = "echo {{name}}";
        model.ParametersText = "name|Name|default";
        using (FileStream held = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await model.ReloadFromDiskCommand.ExecuteAsync(null);
            Assert.True(model.IsStatusError);
            Assert.Single(model.Snippets);
        }
        await model.ReloadFromDiskCommand.ExecuteAsync(null);
        Assert.Equal("draft", model.Name);
        Assert.Equal("echo {{name}}", model.Template);
        Assert.Equal("name|Name|default", model.ParametersText);
        Assert.True(model.HasPendingChanges);
    }

    [Fact]
    public async Task SecretRetryAfterTransientSaveFailure_DoesNotDuplicateDraft()
    {
        string path = Path.Combine(_directory, "secrets.json");
        SecretLibrary library = new(new JsonSecretStore(path), new DpapiSecretProtector());
        await library.LoadAsync();
        SecretManagerViewModel model = new(library, new Locale(), new Confirmation());
        model.Name = "draft";
        model.SetSecretValue("synthetic-value");
        using (FileStream lease = new(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            await model.SaveCommand.ExecuteAsync(null);
            Assert.True(model.IsStatusError);
        }
        await model.SaveCommand.ExecuteAsync(null);
        Assert.False(model.IsStatusError);
        Assert.Single(await new JsonSecretStore(path).LoadAsync());
    }

    [Fact]
    public async Task ConcurrentLibrarySaves_PreserveAllSuccessfulEntries()
    {
        string path = Path.Combine(_directory, "snippets.json");
        SnippetLibrary library = new(new JsonSnippetStore(path));
        await library.LoadAsync();
        Task<bool>[] saves = Enumerable.Range(0, 20).Select(index => Task.Run(() =>
            library.SaveAsync(new Snippet(index.ToString(), "synthetic", null, "text", [])))).ToArray();
        Assert.All(await Task.WhenAll(saves), Assert.True);
        Assert.Equal(20, (await new JsonSnippetStore(path).LoadAsync()).Count);
    }

    [Fact]
    public async Task HistoryRecovery_DeletedRecordDoesNotRedirectDraftToAnotherRecord()
    {
        string path = Path.Combine(_directory, "history.json");
        CaptureHistoryLibrary library = new(new Scrybe.Core.History.JsonCaptureHistoryStore(path), new DpapiSecretProtector(), new AppSettings());
        await library.AddAsync("synthetic-first");
        HistoryManagerViewModel model = new(library, new Clipboard(), new Notification(), new Locale(), new Confirmation());
        model.RevealCommand.Execute(null);
        model.EditText = "synthetic-unsaved";
        CaptureHistoryLibrary competing = new(new Scrybe.Core.History.JsonCaptureHistoryStore(path), new DpapiSecretProtector(), new AppSettings());
        await competing.LoadAsync();
        await competing.ClearAsync();
        await competing.AddAsync("synthetic-other");
        byte[] original = await File.ReadAllBytesAsync(path);
        await model.ReloadFromDiskCommand.ExecuteAsync(null);
        Assert.Equal("synthetic-unsaved", model.EditText);
        Assert.Null(model.SelectedEntry);
        Assert.False(model.SaveEditCommand.CanExecute(null));
        Assert.Single(model.Entries);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task FailedSnippetMutation_DoesNotEnterLibraryOrLaterSave()
    {
        string path = Path.Combine(_directory, "snippets.json");
        SnippetLibrary library = new(new JsonSnippetStore(path));
        await library.SaveAsync(new("first", "first", null, "first", []));
        using (FileStream lease = new(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(await library.SaveAsync(new("draft", "draft", null, "draft", [])));
            Assert.False(await library.DeleteAsync("first"));
            Assert.Equal("first", Assert.Single(library.Snippets).Id);
        }
        Assert.True(await library.SaveAsync(new("other", "other", null, "other", [])));
        Assert.DoesNotContain(await new JsonSnippetStore(path).LoadAsync(), item => item.Id == "draft");
    }

    [Fact]
    public async Task FailedSecretMutation_DoesNotEnterLibraryOrLaterSave()
    {
        string path = Path.Combine(_directory, "secrets.json");
        SecretLibrary library = new(new JsonSecretStore(path), new DpapiSecretProtector());
        SecretSaveResult first = await library.SaveAsync(null, "first", null, "synthetic-first");
        using (FileStream lease = new(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False((await library.SaveAsync(null, "draft", null, "synthetic-draft")).Persisted);
            Assert.False(await library.DeleteAsync(first.Entry.Id));
            Assert.Equal("first", Assert.Single(library.Secrets).Name);
        }
        Assert.True((await library.SaveAsync(null, "other", null, "synthetic-other")).Persisted);
        Assert.DoesNotContain(await new JsonSecretStore(path).LoadAsync(), item => item.Name == "draft");
    }

    [Fact]
    public async Task FailedHistoryMutations_RetainCommittedTextAndEntries()
    {
        string path = Path.Combine(_directory, "history.json");
        CaptureHistoryLibrary library = new(new Scrybe.Core.History.JsonCaptureHistoryStore(path), new DpapiSecretProtector(), new AppSettings());
        await library.AddAsync("synthetic-first");
        string id = Assert.Single(library.Entries).Id;
        using (FileStream lease = new(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(await library.AddAsync("synthetic-draft"));
            Assert.False(await library.UpdateAsync(id, "synthetic-edited"));
            Assert.False(await library.DeleteAsync(id));
            Assert.False(await library.ClearAsync());
            Assert.Equal("synthetic-first", library.RevealText(id));
            Assert.Single(library.Entries);
        }
        Assert.True(await library.AddAsync("synthetic-other"));
        Assert.Equal(2, (await new Scrybe.Core.History.JsonCaptureHistoryStore(path).LoadAsync()).Count);
    }

    [Theory]
    [InlineData(InjectionFailureReason.TargetChanged, "Inject.TargetChanged")]
    [InlineData(InjectionFailureReason.Unmappable, "Inject.Unmappable")]
    [InlineData(InjectionFailureReason.NativeFailure, "Inject.NativeFailure")]
    public async Task Progress_ReportsTargetAndReasonWithoutPayload(InjectionFailureReason reason, string status)
    {
        ProgressInjector injector = new(reason);
        InjectionCoordinator coordinator = new(injector, injector, new OcrTextStore(), new Notification(), new Locale(), new AppSettings());
        List<InjectionProgress> updates = [];
        coordinator.ProgressChanged += (_, progress) => updates.Add(progress);
        await coordinator.InjectSecretAsync("synthetic-secret".ToCharArray(), context: new Context());
        Assert.True(updates[0].IsRunning);
        Assert.Equal("controlled-target", updates[0].Target);
        Assert.False(updates[^1].IsRunning);
        Assert.Equal(1, updates[^1].Completed);
        Assert.Equal(status, updates[^1].StatusKey);
        Assert.DoesNotContain(updates, update => update.ToString().Contains("synthetic-secret", StringComparison.Ordinal));
        coordinator.Abort(); // Finished runs have no live cancellation source.
    }

    [Fact]
    public void ProgressView_StopOnlyEnabledDuringActiveRun()
    {
        int calls = 0;
        InjectionProgressViewModel model = new(new Locale(), () => calls++);
        Assert.False(model.StopCommand.CanExecute(null));
        model.Update(new("test", 3, 10, true, "Inject.Running"));
        Assert.True(model.StopCommand.CanExecute(null));
        model.StopCommand.Execute(null);
        Assert.Equal(1, calls);
        model.Update(new("test", 3, 10, false, "Inject.Aborted"));
        Assert.False(model.StopCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("abc", "abc", 0)]
    [InlineData("abc", "axc", 1d / 3)]
    [InlineData("abc", "ab", 1d / 3)]
    [InlineData("", "ab", 2)]
    public void CharacterErrorRate_CountsInsertionsDeletionsAndSubstitutions(string expected, string actual, double cer)
        => Assert.Equal(cer, TextAccuracy.CharacterErrorRate(expected, actual), 6);

    private sealed class Locale : ILocalizationManager
    {
        public string this[string key] => key == "Inject.ProgressCounts" ? "{0}/{1}" : key;
        public string Current => "en";
        public event EventHandler? LocaleChanged { add { } remove { } }
        public Task LoadAsync(string code, CancellationToken token = default) => Task.CompletedTask;
    }
    private sealed class Confirmation : IConfirmationService
    {
        public bool ConfirmDanger(string title, string message) => true;
    }
    private sealed class Notification : INotificationService
    {
        public void Notify(string title, string message) { }
    }
    private sealed class Clipboard : IClipboardService
    {
        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task SetTextAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class Context : IInjectionContext
    {
        public bool IsCurrent => true;
        public IntPtr KeyboardLayout => new(1);
        public string TargetDisplay => "controlled-target";
    }
    private sealed class ProgressInjector(InjectionFailureReason reason) : IKeystrokeInjector
    {
        public Task<InjectionResult> InjectAsync(KeystrokeSequence sequence, CancellationToken cancellationToken = default, IInjectionContext? context = null)
            => InjectAsync("synthetic".AsMemory(), cancellationToken, context);
        public Task<InjectionResult> InjectAsync(ReadOnlyMemory<char> text, CancellationToken cancellationToken = default, IInjectionContext? context = null)
        {
            context?.ReportProgress(1, text.Length);
            return Task.FromResult(new InjectionResult(false, 2, false, true, reason));
        }
    }
    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
