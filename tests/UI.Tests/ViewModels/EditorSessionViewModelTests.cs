using System.Linq;
using System.Windows.Input;
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
    private readonly FakeMarkdownRoundTrip _roundTrip = new();

    private EditorSessionViewModel CreateSession() => new(_store, _watcher, _dispatcher, _roundTrip);

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
    public async Task ExternalChange_WhenSessionClean_PublishesTheChangeHighlight_INV060()
    {
        var session = await LoadedSessionAsync("alpha\n\nbravo\n\ncharlie");

        _store.Seed(Path, "alpha\n\nBRAVO\n\ncharlie");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.ChangeHighlight.ShouldHaveSingleItem()
            .ShouldBe(new ChangedRegion(ChangedRegionKind.Changed, 2, 1));
    }

    [Fact]
    public async Task ExternalChange_ThatDeletesContent_PublishesTheSeam_INV060()
    {
        var session = await LoadedSessionAsync("alpha\n\nbravo\n\ncharlie");

        _store.Seed(Path, "alpha\n\ncharlie");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.ChangeHighlight.ShouldHaveSingleItem()
            .ShouldBe(new ChangedRegion(ChangedRegionKind.Removed, 2, 0));
    }

    [Fact]
    public async Task ExternalChange_ThatChangesNoContent_PublishesNoChangeHighlight_INV060()
    {
        var session = await LoadedSessionAsync("# Same");

        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.ChangeHighlight.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveConflict_ReloadFromDisk_PublishesTheChangeHighlight_INV060()
    {
        var session = await LoadedSessionAsync("alpha\n\nbravo");
        session.Markdown = "alpha\n\nmy unsaved edit";
        _store.Seed(Path, "alpha\n\nBRAVO");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.ReloadFromDiskCommand.Execute(null);

        // The highlight compares what was on screen — the unsaved edits the user has just given up —
        // with the disk contents that replaced them.
        session.ChangeHighlight.ShouldHaveSingleItem()
            .ShouldBe(new ChangedRegion(ChangedRegionKind.Changed, 2, 1));
    }

    [Fact]
    public async Task ResolveConflict_KeepMyEdits_PublishesNoChangeHighlight_INV060()
    {
        var session = await LoadedSessionAsync("alpha\n\nbravo");
        session.Markdown = "alpha\n\nmy unsaved edit";
        _store.Seed(Path, "alpha\n\nBRAVO");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.KeepMyEditsCommand.Execute(null);

        // Nothing was reloaded, so there is nothing on screen for a highlight to refer to.
        session.ChangeHighlight.ShouldBeEmpty();
    }

    [Fact]
    public async Task EditingMarkdown_ClearsTheChangeHighlight_INV060()
    {
        var session = await LoadedSessionAsync("alpha\n\nbravo");
        _store.Seed(Path, "alpha\n\nBRAVO");
        _watcher.RaiseChanged(Path);
        await Task.Yield();
        session.ChangeHighlight.ShouldNotBeEmpty();

        session.Markdown = "alpha\n\nBRAVO and my own edit";

        session.ChangeHighlight.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadAsync_ClearsTheChangeHighlight_INV060()
    {
        var session = await LoadedSessionAsync("alpha\n\nbravo");
        _store.Seed(Path, "alpha\n\nBRAVO");
        _watcher.RaiseChanged(Path);
        await Task.Yield();
        session.ChangeHighlight.ShouldNotBeEmpty();

        _store.Seed(Path, "something else entirely");
        await session.LoadAsync(Path);

        session.ChangeHighlight.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveAsync_ClearsTheChangeHighlight_INV060()
    {
        var session = await LoadedSessionAsync("alpha\n\nbravo");
        _store.Seed(Path, "alpha\n\nBRAVO");
        _watcher.RaiseChanged(Path);
        await Task.Yield();
        session.ChangeHighlight.ShouldNotBeEmpty();

        await session.SaveAsync(Path);

        session.ChangeHighlight.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExternalChange_PublishesTheChangeHighlightAfterTheNewSource_INV060()
    {
        // The highlight's line numbers index the reloaded text, so the editor must already hold that
        // text when the regions arrive — otherwise they would be resolved against the old document.
        var session = await LoadedSessionAsync("alpha\n\nbravo");
        var markdownWhenHighlightArrived = (string?)null;
        session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(EditorSessionViewModel.ChangeHighlight))
            {
                markdownWhenHighlightArrived = session.Markdown;
            }
        };

        _store.Seed(Path, "alpha\n\nBRAVO");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        markdownWhenHighlightArrived.ShouldBe("alpha\n\nBRAVO");
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
    public async Task ExternalChange_ThatMatchesSession_IsIgnored_INV026()
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

    [Fact]
    public async Task ExternalChange_RaisingConflict_RequeriesTheConflictCommands()
    {
        var session = await LoadedSessionAsync("# Original");
        session.Markdown = "# My unsaved edit";
        var requeried = CountRequeries(session);

        _store.Seed(Path, "# Disk change");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.HasConflict.ShouldBeTrue();
        requeried().ShouldAllBe(count => count > 0);
    }

    [Fact]
    public async Task ResolveConflict_KeepMyEdits_RequeriesTheConflictCommands()
    {
        var session = await ConflictedSessionAsync();
        var requeried = CountRequeries(session);

        session.KeepMyEditsCommand.Execute(null);

        session.HasConflict.ShouldBeFalse();
        requeried().ShouldAllBe(count => count > 0);
    }

    /// <summary>
    /// Stands in for Capture's normalisation: a Watched File written with a setext heading and `_`
    /// emphasis Round-Trips to the same Canonical Markdown as the ATX-and-`*` form the Editor
    /// Session holds. Leaves already-canonical text alone, as a real Round-Trip does (INV-005).
    /// </summary>
    private void CanonicaliseSetextAndUnderscores() =>
        _roundTrip.Canonicalise = markdown => markdown
            .Replace("Title\n=====", "# Title")
            .Replace("_there_", "*there*");

    [Fact]
    public async Task ExternalChange_ThatOnlyRestylesTheWatchedFile_WithUnsavedEdits_RaisesNoConflict_INV026()
    {
        CanonicaliseSetextAndUnderscores();
        var session = await LoadedSessionAsync("# Title\n\nHello *everyone*");
        session.Markdown = "# Title\n\nHello *there*";

        // The other writer restyles the file to say exactly what the session already says.
        _store.Seed(Path, "Title\n=====\n\nHello _there_");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.HasConflict.ShouldBeFalse();
        session.Markdown.ShouldBe("# Title\n\nHello *there*");
        session.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public async Task ExternalChange_ThatOnlyRestylesTheWatchedFile_WhenSessionClean_IsIgnored_INV026()
    {
        CanonicaliseSetextAndUnderscores();
        var session = await LoadedSessionAsync("Title\n=====\n\nHello _there_");

        _store.Seed(Path, "# Title\n\nHello *there*");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        // No content changed, so the Visual Document is not re-projected out from under the user.
        session.Markdown.ShouldBe("Title\n=====\n\nHello _there_");
        session.HasConflict.ShouldBeFalse();
        session.HasUnsavedEdits.ShouldBeFalse();
    }

    [Fact]
    public async Task ViewDifference_StillShowsARealChange_BesideChurn_INV025()
    {
        CanonicaliseSetextAndUnderscores();
        var session = await LoadedSessionAsync("# Title\n\nHello *there*\n\nOriginal line.");
        session.Markdown = "# Title\n\nHello *there*\n\nMy new line.";
        _store.Seed(Path, "Title\n=====\n\nHello _there_\n\nTheir old line.");
        _watcher.RaiseChanged(Path);
        await Task.Yield();

        session.ViewDifferenceCommand.Execute(null);

        session.DifferenceLines
            .Where(line => line.Kind != DifferenceLineKind.Unchanged)
            .ShouldBe(
            [
                new DifferenceLine(DifferenceLineKind.SessionOnly, "My new line."),
                new DifferenceLine(DifferenceLineKind.DiskOnly, "Their old line."),
            ]);
    }

    /// <summary>
    /// Subscribes to the three conflict-bar commands and returns a probe for how many times each
    /// has since asked to be requeried.
    /// </summary>
    private static Func<IReadOnlyList<int>> CountRequeries(EditorSessionViewModel session)
    {
        var counts = new int[3];
        ICommand[] commands =
            [session.ViewDifferenceCommand, session.KeepMyEditsCommand, session.ReloadFromDiskCommand];

        for (var i = 0; i < commands.Length; i++)
        {
            var index = i;
            commands[i].CanExecuteChanged += (_, _) => counts[index]++;
        }

        return () => counts;
    }
}
