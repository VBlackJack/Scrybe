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
using Scrybe.App.ViewModels;
using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Xunit;

namespace Scrybe.Core.Tests;

/// <summary>Tests for App manager view-model guard rails that protect persisted user data.</summary>
public sealed class ManagerViewModelTests
{
    [Fact]
    public async Task SecretManager_DeleteCancelled_KeepsSecretAndDoesNotPersistDelete()
    {
        InMemorySecretStore store = new();
        SecretLibrary library = new(store, new TestSecretProtector());
        await library.SaveAsync(null, "Production login", "admin", "secret");
        int saveCountAfterArrange = store.SaveCount;
        DenyingConfirmationService confirmation = new();
        SecretManagerViewModel viewModel = new(library, new TestLocalizationManager(), confirmation)
        {
            SelectedSecret = library.Secrets.Single(),
        };

        await viewModel.DeleteCommand.ExecuteAsync(null);

        confirmation.CallCount.Should().Be(1);
        library.Secrets.Should().ContainSingle(secret => secret.Name == "Production login");
        store.SavedSecrets.Should().ContainSingle(secret => secret.Name == "Production login");
        store.SaveCount.Should().Be(saveCountAfterArrange);
    }

    [Fact]
    public async Task SnippetManager_DeleteCancelled_KeepsSnippetAndDoesNotPersistDelete()
    {
        InMemorySnippetStore store = new();
        SnippetLibrary library = new(store);
        Snippet snippet = new("snippet-1", "Package acceptance", null, "apt install {{package}}", []);
        await library.SaveAsync(snippet);
        int saveCountAfterArrange = store.SaveCount;
        DenyingConfirmationService confirmation = new();
        SnippetManagerViewModel viewModel = new(library, new TestLocalizationManager(), confirmation)
        {
            SelectedSnippet = library.Snippets.Single(),
        };

        await viewModel.DeleteCommand.ExecuteAsync(null);

        confirmation.CallCount.Should().Be(1);
        library.Snippets.Should().ContainSingle(existing => existing.Name == "Package acceptance");
        store.SavedSnippets.Should().ContainSingle(existing => existing.Name == "Package acceptance");
        store.SaveCount.Should().Be(saveCountAfterArrange);
    }

    [Fact]
    public async Task CaptureHistory_ClearAllCancelled_KeepsHistoryAndDoesNotPersistClear()
    {
        InMemoryCaptureHistoryStore store = new();
        CaptureHistoryLibrary library = new(store, new TestSecretProtector(), new AppSettings());
        await library.AddAsync("first capture");
        int saveCountAfterArrange = store.SaveCount;
        DenyingConfirmationService confirmation = new();
        CaptureHistoryCoordinator coordinator = new(
            library,
            new TestClipboardService(),
            new TestNotificationService(),
            new TestLocalizationManager(),
            confirmation);

        bool cleared = await coordinator.ClearAllAsync();

        cleared.Should().BeFalse();
        confirmation.CallCount.Should().Be(1);
        library.Entries.Should().ContainSingle();
        store.SavedEntries.Should().ContainSingle();
        store.SaveCount.Should().Be(saveCountAfterArrange);
    }

    [Fact]
    public async Task HistoryManager_DeleteCancelled_KeepsHistoryAndDoesNotPersistDelete()
    {
        InMemoryCaptureHistoryStore store = new();
        CaptureHistoryLibrary library = new(store, new TestSecretProtector(), new AppSettings());
        await library.AddAsync("managed history entry");
        int saveCountAfterArrange = store.SaveCount;
        DenyingConfirmationService confirmation = new();
        HistoryManagerViewModel viewModel = new(
            library,
            new TestClipboardService(),
            new TestNotificationService(),
            new TestLocalizationManager(),
            confirmation);

        await viewModel.DeleteCommand.ExecuteAsync(null);

        confirmation.CallCount.Should().Be(1);
        library.Entries.Should().ContainSingle();
        store.SavedEntries.Should().ContainSingle();
        store.SaveCount.Should().Be(saveCountAfterArrange);
    }

    [Fact]
    public async Task HistoryManager_ClearAllCancelled_KeepsHistoryAndDoesNotPersistClear()
    {
        InMemoryCaptureHistoryStore store = new();
        CaptureHistoryLibrary library = new(store, new TestSecretProtector(), new AppSettings());
        await library.AddAsync("managed history entry");
        int saveCountAfterArrange = store.SaveCount;
        DenyingConfirmationService confirmation = new();
        HistoryManagerViewModel viewModel = new(
            library,
            new TestClipboardService(),
            new TestNotificationService(),
            new TestLocalizationManager(),
            confirmation);

        await viewModel.ClearAllCommand.ExecuteAsync(null);

        confirmation.CallCount.Should().Be(1);
        library.Entries.Should().ContainSingle();
        store.SavedEntries.Should().ContainSingle();
        store.SaveCount.Should().Be(saveCountAfterArrange);
    }

    [Fact]
    public async Task HistoryManager_CopySelected_CopiesRevealedText()
    {
        InMemoryCaptureHistoryStore store = new();
        CaptureHistoryLibrary library = new(store, new TestSecretProtector(), new AppSettings());
        await library.AddAsync("managed history entry");
        TestClipboardService clipboard = new();
        TestNotificationService notification = new();
        HistoryManagerViewModel viewModel = new(
            library,
            clipboard,
            notification,
            new TestLocalizationManager(),
            new DenyingConfirmationService());

        await viewModel.CopyCommand.ExecuteAsync(null);

        clipboard.Text.Should().Be("managed history entry");
        notification.CallCount.Should().Be(1);
        viewModel.IsStatusError.Should().BeFalse();
    }

    [Fact]
    public async Task HistoryManager_SelectedPreview_UsesFullRevealedText()
    {
        InMemoryCaptureHistoryStore store = new();
        CaptureHistoryLibrary library = new(store, new TestSecretProtector(), new AppSettings());
        string text = string.Concat(Enumerable.Repeat("managed history long entry ", 8));
        await library.AddAsync(text);
        HistoryManagerViewModel viewModel = new(
            library,
            new TestClipboardService(),
            new TestNotificationService(),
            new TestLocalizationManager(),
            new DenyingConfirmationService());

        viewModel.Entries.Should().ContainSingle();
        viewModel.Entries.Single().Preview.Should().NotBe(text);
        viewModel.SelectedPreview.Should().Be(text);
        viewModel.EditText.Should().Be(text);
    }

    [Fact]
    public async Task CaptureHistory_UpdateAsync_PreservesEntryIdentityTimestampAndPosition()
    {
        InMemoryCaptureHistoryStore store = new();
        DateTimeOffset capturedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        CaptureHistoryEntry newest = new("newest", "protected:newest text", 11, DateTimeOffset.UtcNow);
        CaptureHistoryEntry edited = new("edited", "protected:old text", 8, capturedAt);
        CaptureHistoryEntry oldest = new("oldest", "protected:oldest text", 11, DateTimeOffset.UtcNow.AddMinutes(-5));
        await store.SaveAsync([newest, edited, oldest]);
        CaptureHistoryLibrary library = new(store, new TestSecretProtector(), new AppSettings());
        await library.LoadAsync();

        await library.UpdateAsync("edited", "new edited history text");

        library.Entries.Select(entry => entry.Id).Should().Equal("newest", "edited", "oldest");
        CaptureHistoryEntry updated = library.Entries[1];
        updated.Id.Should().Be("edited");
        updated.CapturedAtUtc.Should().Be(capturedAt);
        updated.CharCount.Should().Be("new edited history text".Length);
        updated.ProtectedText.Should().Be("protected:new edited history text");
        library.RevealText("edited").Should().Be("new edited history text");
        store.SavedEntries.Select(entry => entry.Id).Should().Equal("newest", "edited", "oldest");
    }

    [Fact]
    public async Task HistoryManager_SaveEdit_UpdatesStoredTextAndKeepsSelection()
    {
        InMemoryCaptureHistoryStore store = new();
        CaptureHistoryLibrary library = new(store, new TestSecretProtector(), new AppSettings());
        await library.AddAsync("managed history original");
        HistoryManagerViewModel viewModel = new(
            library,
            new TestClipboardService(),
            new TestNotificationService(),
            new TestLocalizationManager(),
            new DenyingConfirmationService());
        string selectedId = viewModel.SelectedEntry!.Id;

        viewModel.EditText = "managed history edited";
        await viewModel.SaveEditCommand.ExecuteAsync(null);

        viewModel.SelectedEntry!.Id.Should().Be(selectedId);
        viewModel.EditText.Should().Be("managed history edited");
        viewModel.SelectedPreview.Should().Be("managed history edited");
        viewModel.Entries.Should().ContainSingle(entry => entry.Id == selectedId && entry.CharCount == "managed history edited".Length);
        library.RevealText(selectedId).Should().Be("managed history edited");
        store.SavedEntries.Should().ContainSingle(entry => entry.Id == selectedId && entry.ProtectedText == "protected:managed history edited");
        viewModel.StatusMessage.Should().Be("Saved");
        viewModel.IsStatusError.Should().BeFalse();
    }

    [Fact]
    public async Task HistoryManager_SaveEdit_WhenStoreFails_SurfacesPersistenceFailure()
    {
        InMemoryCaptureHistoryStore store = new();
        CaptureHistoryLibrary library = new(store, new TestSecretProtector(), new AppSettings());
        await library.AddAsync("managed history original");
        store.SaveSucceeds = false;
        TestNotificationService notification = new();
        HistoryManagerViewModel viewModel = new(
            library,
            new TestClipboardService(),
            notification,
            new TestLocalizationManager(),
            new DenyingConfirmationService());

        viewModel.EditText = "managed history edited";
        await viewModel.SaveEditCommand.ExecuteAsync(null);

        viewModel.StatusMessage.Should().Be("Couldn't save changes");
        viewModel.IsStatusError.Should().BeTrue();
        notification.Messages.Should().ContainSingle("Couldn't save changes");
    }

    [Fact]
    public async Task HistoryManager_EmptyEdit_DisablesSaveAndDoesNotPersist()
    {
        InMemoryCaptureHistoryStore store = new();
        CaptureHistoryLibrary library = new(store, new TestSecretProtector(), new AppSettings());
        await library.AddAsync("managed history original");
        int saveCountAfterArrange = store.SaveCount;
        HistoryManagerViewModel viewModel = new(
            library,
            new TestClipboardService(),
            new TestNotificationService(),
            new TestLocalizationManager(),
            new DenyingConfirmationService());
        string selectedId = viewModel.SelectedEntry!.Id;

        viewModel.EditText = "   ";

        viewModel.CanSaveEdit.Should().BeFalse();
        viewModel.SaveEditCommand.CanExecute(null).Should().BeFalse();
        store.SaveCount.Should().Be(saveCountAfterArrange);
        library.RevealText(selectedId).Should().Be("managed history original");
    }

    private sealed class DenyingConfirmationService : IConfirmationService
    {
        public int CallCount { get; private set; }

        public bool ConfirmDanger(string title, string message)
        {
            CallCount++;
            return false;
        }
    }

    private sealed class TestLocalizationManager : ILocalizationManager
    {
        public string this[string key] => key switch
        {
            "Manager.DeleteConfirmTitle" => "Delete snippet",
            "Manager.DeleteConfirmMessage" => "Delete snippet \"{0}\"?",
            "Secrets.DeleteConfirmTitle" => "Delete secret",
            "Secrets.DeleteConfirmMessage" => "Delete secret \"{0}\"?",
            "History.DeleteConfirmTitle" => "Delete history entry",
            "History.DeleteConfirmMessage" => "Delete history entry from {0}?",
            "History.ClearConfirmTitle" => "Clear history",
            "History.ClearConfirmMessage" => "Clear history?",
            "History.Copied" => "{0} characters copied",
            "History.Missing" => "Missing",
            "History.CopyFailed" => "Copy failed",
            "History.Saved" => "Saved",
            "History.EmptyTextError" => "Empty text",
            "History.SaveFailed" => "Save failed",
            "History.Deleted" => "Deleted",
            "History.DeleteFailed" => "Delete failed",
            "History.Cleared" => "Cleared",
            "History.ClearFailed" => "Clear failed",
            "Notify.CopiedFromHistory" => "{0} characters copied from history",
            "Persist.SaveFailed" => "Couldn't save changes",
            "AppTitle" => "Scrybe",
            _ => key,
        };

        public string Current => "en";

        public event EventHandler? LocaleChanged
        {
            add { }
            remove { }
        }

        public Task LoadAsync(string localeCode, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestSecretProtector : ISecretProtector
    {
        public string Protect(string secret) => $"protected:{secret}";

        public char[] UnprotectToChars(string protectedSecret) =>
            protectedSecret["protected:".Length..].ToCharArray();
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        public List<SecretEntry> SavedSecrets { get; private set; } = [];

        public int SaveCount { get; private set; }

        public bool SaveSucceeds { get; set; } = true;

        public Task<IReadOnlyList<SecretEntry>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SecretEntry>>(SavedSecrets);

        public Task<bool> SaveAsync(IReadOnlyList<SecretEntry> secrets, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (!SaveSucceeds)
            {
                return Task.FromResult(false);
            }

            SavedSecrets = [.. secrets];
            return Task.FromResult(true);
        }
    }

    private sealed class InMemoryCaptureHistoryStore : ICaptureHistoryStore
    {
        public List<CaptureHistoryEntry> SavedEntries { get; private set; } = [];

        public int SaveCount { get; private set; }

        public bool SaveSucceeds { get; set; } = true;

        public Task<IReadOnlyList<CaptureHistoryEntry>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CaptureHistoryEntry>>(SavedEntries);

        public Task<bool> SaveAsync(IReadOnlyList<CaptureHistoryEntry> entries, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (!SaveSucceeds)
            {
                return Task.FromResult(false);
            }

            SavedEntries = [.. entries];
            return Task.FromResult(true);
        }
    }

    private sealed class InMemorySnippetStore : ISnippetStore
    {
        public List<Snippet> SavedSnippets { get; private set; } = [];

        public int SaveCount { get; private set; }

        public bool SaveSucceeds { get; set; } = true;

        public Task<IReadOnlyList<Snippet>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Snippet>>(SavedSnippets);

        public Task<bool> SaveAsync(IReadOnlyList<Snippet> snippets, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (!SaveSucceeds)
            {
                return Task.FromResult(false);
            }

            SavedSnippets = [.. snippets];
            return Task.FromResult(true);
        }
    }

    private sealed class TestClipboardService : IClipboardService
    {
        public string? Text { get; private set; }

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Text);

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            Text = text;
            return Task.CompletedTask;
        }
    }

    private sealed class TestNotificationService : INotificationService
    {
        public List<string> Messages { get; } = [];

        public int CallCount => Messages.Count;

        public void Notify(string title, string message)
        {
            Messages.Add(message);
        }
    }
}
