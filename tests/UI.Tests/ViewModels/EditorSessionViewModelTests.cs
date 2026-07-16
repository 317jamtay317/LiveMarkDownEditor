using Domain;
using Shouldly;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="EditorSessionViewModel"/> — one Tab's Editor Session: its Markdown Document
/// source, its Watched File, unsaved-edit tracking, load/save, External Change handling
/// (INV-006/007), and View Difference over a Conflict (INV-021). File selection and Tab management
/// live in <see cref="WorkspaceViewModel"/>.
/// </summary>
public sealed class EditorSessionViewModelTests
{
    private const string Path = @"C:\docs\note.md";

    private readonly FakeDocumentStore _store = new();
    private readonly FakeDocumentWatcher _watcher = new();
    private readonly InlineUiDispatcher _dispatcher = new();

    private EditorSessionViewModel CreateSession() => new(_store, _watcher, _dispatcher);

    private async Task<EditorSessionViewModel> LoadedSessionAsync(string content)
    {
        _store.Seed(Path, content);
        var session = CreateSession();
        await session.LoadAsync(Path);
        return session;
    }

    [Fact]
    public void Constructed_StartsEmptyWithNoWatchedFileAndNoUnsavedEdits()
    {
        var session = CreateSession();

        session.Markdown.ShouldBe("");
        session.FilePath.ShouldBeNull();
        session.HasUnsavedEdits.ShouldBeFalse();
        session.Name.ShouldBe("Untitled");
    }

    [Fact]
    public void EditingMarkdown_MarksUnsavedEdits()
    {
        var session = CreateSession();

        session.Markdown = "# Edited";

        session.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadAsync_LoadsFileContent_SetsWatchedFile_AndStartsWatching()
    {
        var session = await LoadedSessionAsync("# Loaded\n\nBody.");

        session.Markdown.ShouldBe("# Loaded\n\nBody.");
        session.FilePath.ShouldBe(Path);
        session.Name.ShouldBe("note.md");
        session.HasUnsavedEdits.ShouldBeFalse();
        _watcher.WatchedPath.ShouldBe(Path);
    }

    [Fact]
    public async Task SaveAsync_ToPath_PersistsAndClearsUnsavedEdits_AndWatches()
    {
        var session = CreateSession();
        session.Markdown = "# Brand new";

        await session.SaveAsync(Path);

        _store.SavedText(Path).ShouldBe("# Brand new");
        session.FilePath.ShouldBe(Path);
        session.HasUnsavedEdits.ShouldBeFalse();
        _watcher.WatchedPath.ShouldBe(Path);
    }

    [Fact]
    public async Task Dispose_StopsWatching()
    {
        var session = await LoadedSessionAsync("# Loaded");
        _watcher.WatchedPath.ShouldBe(Path);

        session.Dispose();

        _watcher.WatchedPath.ShouldBeNull();
    }

    [Fact]
    public async Task ExternalChange_WhenSessionClean_ReloadsLive_INV007()
    {
        var session = await LoadedSessionAsync("# Original");

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
        var session = await LoadedSessionAsync("# Original");
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
        var session = await LoadedSessionAsync("# Same");

        // Simulate the watcher firing for the app's own save (disk == session content).
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.HasConflict.ShouldBeFalse();
    }

    [Fact]
    public async Task ResolveConflict_ReloadFromDisk_DiscardsEditsAndLoadsDisk()
    {
        var session = await LoadedSessionAsync("# Original");
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
        var session = await LoadedSessionAsync("# Original");
        session.Markdown = "# My edit";
        _store.Seed(Path, "# Disk change");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.KeepMyEditsCommand.Execute(null);

        session.Markdown.ShouldBe("# My edit");
        session.HasConflict.ShouldBeFalse();
        session.HasUnsavedEdits.ShouldBeTrue();
    }

    private async Task<EditorSessionViewModel> ConflictedSessionAsync(
        string edit = "# Mine",
        string disk = "# Disk")
    {
        var session = await LoadedSessionAsync("# Original");
        session.Markdown = edit;
        _store.Seed(Path, disk);
        _watcher.RaiseChanged(Path);
        await Task.Yield();
        return session;
    }

    [Fact]
    public async Task ViewDifference_ShowsDifference_WithoutChangingMarkdownOrConflict_INV021()
    {
        var session = await ConflictedSessionAsync();

        session.ViewDifferenceCommand.Execute(null);

        session.IsDifferenceVisible.ShouldBeTrue();
        session.DifferenceLines.ShouldContain(
            new DifferenceLine(DifferenceLineKind.SessionOnly, "# Mine"));
        session.DifferenceLines.ShouldContain(
            new DifferenceLine(DifferenceLineKind.DiskOnly, "# Disk"));
        session.Markdown.ShouldBe("# Mine");
        session.HasConflict.ShouldBeTrue();
        session.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public async Task ViewDifference_ExecutedTwice_HidesTheDifference()
    {
        var session = await ConflictedSessionAsync();

        session.ViewDifferenceCommand.Execute(null);
        session.ViewDifferenceCommand.Execute(null);

        session.IsDifferenceVisible.ShouldBeFalse();
        session.DifferenceLines.ShouldBeEmpty();
        session.HasConflict.ShouldBeTrue();
    }

    [Fact]
    public void ViewDifference_WithoutConflict_CannotExecute()
    {
        var session = CreateSession();

        session.ViewDifferenceCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task ResolveConflict_KeepMyEdits_HidesDifference()
    {
        var session = await ConflictedSessionAsync();
        session.ViewDifferenceCommand.Execute(null);

        session.KeepMyEditsCommand.Execute(null);

        session.IsDifferenceVisible.ShouldBeFalse();
        session.DifferenceLines.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveConflict_ReloadFromDisk_HidesDifference()
    {
        var session = await ConflictedSessionAsync();
        session.ViewDifferenceCommand.Execute(null);

        session.ReloadFromDiskCommand.Execute(null);

        session.IsDifferenceVisible.ShouldBeFalse();
        session.DifferenceLines.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExternalChange_WhileDifferenceVisible_RefreshesDifferenceLines()
    {
        var session = await ConflictedSessionAsync();
        session.ViewDifferenceCommand.Execute(null);

        _store.Seed(Path, "# Disk again");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.DifferenceLines.ShouldContain(
            new DifferenceLine(DifferenceLineKind.DiskOnly, "# Disk again"));
        session.DifferenceLines.ShouldNotContain(
            new DifferenceLine(DifferenceLineKind.DiskOnly, "# Disk"));
    }

    [Fact]
    public async Task EditingMarkdown_WhileDifferenceVisible_RefreshesDifferenceLines()
    {
        var session = await ConflictedSessionAsync();
        session.ViewDifferenceCommand.Execute(null);

        session.Markdown = "# Mine again";

        session.DifferenceLines.ShouldContain(
            new DifferenceLine(DifferenceLineKind.SessionOnly, "# Mine again"));
        session.DifferenceLines.ShouldNotContain(
            new DifferenceLine(DifferenceLineKind.SessionOnly, "# Mine"));
    }
}
