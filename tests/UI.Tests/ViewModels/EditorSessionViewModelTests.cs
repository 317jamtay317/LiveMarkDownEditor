using Shouldly;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="EditorSessionViewModel"/> — the Editor Session realised for the UI:
/// the current Markdown Document source, its Watched File, unsaved-edit tracking, the open/save/new
/// behaviours, and External Change handling (INV-006/007).
/// </summary>
public sealed class EditorSessionViewModelTests
{
    private const string Path = @"C:\docs\note.md";

    private readonly FakeDocumentStore _store = new();
    private readonly StubFilePicker _picker = new();
    private readonly FakeDocumentWatcher _watcher = new();
    private readonly InlineUiDispatcher _dispatcher = new();

    private EditorSessionViewModel CreateSession() => new(_store, _picker, _watcher, _dispatcher);

    private async Task<EditorSessionViewModel> OpenedSessionAsync(string content)
    {
        _store.Seed(Path, content);
        _picker.OpenResult = Path;
        var session = CreateSession();
        await session.OpenAsync();
        return session;
    }

    [Fact]
    public void New_StartsEmptyWithNoWatchedFileAndNoUnsavedEdits()
    {
        var session = CreateSession();

        session.Markdown.ShouldBe("");
        session.FilePath.ShouldBeNull();
        session.HasUnsavedEdits.ShouldBeFalse();
    }

    [Fact]
    public void EditingMarkdown_MarksUnsavedEdits()
    {
        var session = CreateSession();

        session.Markdown = "# Edited";

        session.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public async Task OpenAsync_LoadsFileContent_SetsWatchedFile_AndStartsWatching()
    {
        var session = await OpenedSessionAsync("# Loaded\n\nBody.");

        session.Markdown.ShouldBe("# Loaded\n\nBody.");
        session.FilePath.ShouldBe(Path);
        session.HasUnsavedEdits.ShouldBeFalse();
        _watcher.WatchedPath.ShouldBe(Path);
    }

    [Fact]
    public async Task OpenAsync_WhenUserCancels_LeavesSessionUnchanged()
    {
        _picker.OpenResult = null;
        var session = CreateSession();
        session.Markdown = "# Keep me";

        await session.OpenAsync();

        session.Markdown.ShouldBe("# Keep me");
    }

    [Fact]
    public async Task SaveAsync_WithKnownWatchedFile_PersistsAndClearsUnsavedEdits()
    {
        var session = await OpenedSessionAsync("old");
        session.Markdown = "# Changed";

        await session.SaveAsync();

        _store.SavedText(Path).ShouldBe("# Changed");
        session.HasUnsavedEdits.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_WithNoWatchedFile_PromptsForPath_ThenPersists()
    {
        _picker.SaveResult = @"C:\docs\new.md";
        var session = CreateSession();
        session.Markdown = "# Brand new";

        await session.SaveAsync();

        _store.SavedText(@"C:\docs\new.md").ShouldBe("# Brand new");
        session.FilePath.ShouldBe(@"C:\docs\new.md");
        session.HasUnsavedEdits.ShouldBeFalse();
    }

    [Fact]
    public async Task ExternalChange_WhenSessionClean_ReloadsLive_INV007()
    {
        var session = await OpenedSessionAsync("# Original");

        _store.Seed(Path, "# Changed by AI");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.Markdown.ShouldBe("# Changed by AI");
        session.HasConflict.ShouldBeFalse();
        session.HasUnsavedEdits.ShouldBeFalse();
    }

    [Fact]
    public async Task ExternalChange_WithUnsavedEdits_RaisesConflict_AndKeepsEdits_INV006()
    {
        var session = await OpenedSessionAsync("# Original");
        session.Markdown = "# My unsaved edit";

        _store.Seed(Path, "# Changed on disk");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.HasConflict.ShouldBeTrue();
        session.Markdown.ShouldBe("# My unsaved edit");
    }

    [Fact]
    public async Task ExternalChange_ThatMatchesSession_IsIgnored_NoConflict()
    {
        var session = await OpenedSessionAsync("# Same");

        // Simulate the watcher firing for the app's own save (disk == session content).
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.HasConflict.ShouldBeFalse();
    }

    [Fact]
    public async Task ResolveConflict_ReloadFromDisk_DiscardsEditsAndLoadsDisk()
    {
        var session = await OpenedSessionAsync("# Original");
        session.Markdown = "# My edit";
        _store.Seed(Path, "# Disk wins");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.ReloadFromDiskCommand.Execute(null);

        session.Markdown.ShouldBe("# Disk wins");
        session.HasConflict.ShouldBeFalse();
        session.HasUnsavedEdits.ShouldBeFalse();
    }

    [Fact]
    public async Task ResolveConflict_KeepMyEdits_KeepsEditsAndClearsConflict()
    {
        var session = await OpenedSessionAsync("# Original");
        session.Markdown = "# My edit";
        _store.Seed(Path, "# Disk change");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.KeepMyEditsCommand.Execute(null);

        session.Markdown.ShouldBe("# My edit");
        session.HasConflict.ShouldBeFalse();
        session.HasUnsavedEdits.ShouldBeTrue();
    }
}
