using Shouldly;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="WorkspaceViewModel"/> — the editor shell that holds Editor Sessions as Tabs:
/// the New / Open / Save / Close behaviours, Active Session tracking, and the Workspace invariants
/// INV-008 (never empty), INV-009 (a file is open in at most one Tab), and INV-010 (closing with
/// unsaved edits is never silent).
/// </summary>
public sealed class WorkspaceViewModelTests
{
    private const string Path = @"C:\docs\note.md";
    private const string OtherPath = @"C:\docs\other.md";

    private readonly FakeDocumentStore _store = new();
    private readonly StubFilePicker _picker = new();
    private readonly StubUnsavedEditsPrompt _prompt = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly FakeThemeService _theme = new();
    private readonly FakeMarkdownRoundTrip _roundTrip = new();
    private readonly List<FakeDocumentWatcher> _watchers = [];

    private WorkspaceViewModel CreateWorkspace()
    {
        EditorSessionFactory factory = () =>
        {
            var watcher = new FakeDocumentWatcher();
            _watchers.Add(watcher);
            return new EditorSessionViewModel(_store, watcher, _dispatcher, _roundTrip);
        };
        return new WorkspaceViewModel(factory, _picker, _prompt, new AppearanceViewModel(_theme));
    }

    [Fact]
    public void Constructor_StartsWithOneEmptyActiveSession_INV008()
    {
        var workspace = CreateWorkspace();

        workspace.Sessions.Count.ShouldBe(1);
        workspace.ActiveSession.ShouldBe(workspace.Sessions[0]);
        workspace.ActiveSession!.Markdown.ShouldBe("");
        workspace.ActiveSession.FilePath.ShouldBeNull();
    }

    [Fact]
    public void Constructor_StartsWithNavigationPanelHidden()
    {
        var workspace = CreateWorkspace();

        // The Navigation Panel is hidden until the user toggles it on.
        workspace.IsNavigationPanelVisible.ShouldBeFalse();
    }

    [Fact]
    public void ToggleNavigationPanel_TogglesItsVisibility()
    {
        var workspace = CreateWorkspace();

        workspace.ToggleNavigationPanelCommand.Execute(null);
        workspace.IsNavigationPanelVisible.ShouldBeTrue();

        workspace.ToggleNavigationPanelCommand.Execute(null);
        workspace.IsNavigationPanelVisible.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_StartsWithSourcePanelHidden_INV014()
    {
        var workspace = CreateWorkspace();

        // The Source Panel is hidden until the user toggles it on.
        workspace.IsSourcePanelVisible.ShouldBeFalse();
    }

    [Fact]
    public void ToggleSourcePanel_TogglesVisibility_WithoutChangingDocument_INV014()
    {
        var workspace = CreateWorkspace();
        workspace.ActiveSession!.Markdown = "# Title";
        var sourceBefore = workspace.ActiveSession.Markdown;

        workspace.ToggleSourcePanelCommand.Execute(null);
        workspace.IsSourcePanelVisible.ShouldBeTrue();

        workspace.ToggleSourcePanelCommand.Execute(null);
        workspace.IsSourcePanelVisible.ShouldBeFalse();

        // Toggling the Source Panel is view-only: the Markdown Document is untouched (INV-014).
        workspace.ActiveSession.Markdown.ShouldBe(sourceBefore);
    }

    [Fact]
    public void New_AddsEmptySession_AndActivatesIt()
    {
        var workspace = CreateWorkspace();

        workspace.New();

        workspace.Sessions.Count.ShouldBe(2);
        workspace.ActiveSession.ShouldBe(workspace.Sessions[1]);
        workspace.ActiveSession!.Markdown.ShouldBe("");
    }

    [Fact]
    public async Task Open_LoadsFileIntoNewTab_AndActivatesIt()
    {
        _store.Seed(Path, "# Loaded");
        _picker.OpenResult = Path;
        var workspace = CreateWorkspace();

        await workspace.OpenAsync();

        workspace.Sessions.Count.ShouldBe(2);
        workspace.ActiveSession!.FilePath.ShouldBe(Path);
        workspace.ActiveSession.Markdown.ShouldBe("# Loaded");
        workspace.ActiveSession.Name.ShouldBe("note.md");
    }

    [Fact]
    public async Task Open_WhenFileAlreadyOpen_ActivatesExistingTab_INV009()
    {
        _store.Seed(Path, "# Loaded");
        _picker.OpenResult = Path;
        var workspace = CreateWorkspace();
        await workspace.OpenAsync();
        var firstOpen = workspace.ActiveSession;
        workspace.New();

        await workspace.OpenAsync();

        workspace.Sessions.Count.ShouldBe(3); // initial empty + opened + new empty; no duplicate
        workspace.ActiveSession.ShouldBe(firstOpen);
    }

    [Fact]
    public async Task OpenPath_LoadsFileIntoNewTab_AndActivatesIt_INV020()
    {
        // A Startup Document arrives as a path (no picker involved) and opens like any other file.
        _store.Seed(Path, "# Loaded");
        var workspace = CreateWorkspace();

        await workspace.OpenPathAsync(Path);

        workspace.Sessions.Count.ShouldBe(2);
        workspace.ActiveSession!.FilePath.ShouldBe(Path);
        workspace.ActiveSession.Markdown.ShouldBe("# Loaded");
    }

    [Fact]
    public async Task OpenPath_WhenFileAlreadyOpen_ActivatesExistingTab_INV009_INV020()
    {
        _store.Seed(Path, "# Loaded");
        var workspace = CreateWorkspace();
        await workspace.OpenPathAsync(Path);
        var firstOpen = workspace.ActiveSession;
        workspace.New();

        await workspace.OpenPathAsync(Path);

        workspace.Sessions.Count.ShouldBe(3); // initial empty + opened + new empty; no duplicate
        workspace.ActiveSession.ShouldBe(firstOpen);
    }

    [Fact]
    public async Task Open_WhenUserCancels_AddsNoTab()
    {
        _picker.OpenResult = null;
        var workspace = CreateWorkspace();

        await workspace.OpenAsync();

        workspace.Sessions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Save_ActiveSession_WithKnownPath_Persists()
    {
        _store.Seed(Path, "old");
        _picker.OpenResult = Path;
        var workspace = CreateWorkspace();
        await workspace.OpenAsync();
        workspace.ActiveSession!.Markdown = "# Changed";

        await workspace.SaveActiveAsync();

        _store.SavedText(Path).ShouldBe("# Changed");
        workspace.ActiveSession.HasUnsavedEdits.ShouldBeFalse();
    }

    [Fact]
    public async Task Save_ActiveSession_WithNoPath_PromptsThenPersists()
    {
        _picker.SaveResult = OtherPath;
        var workspace = CreateWorkspace();
        workspace.ActiveSession!.Markdown = "# Brand new";

        await workspace.SaveActiveAsync();

        _store.SavedText(OtherPath).ShouldBe("# Brand new");
        workspace.ActiveSession.FilePath.ShouldBe(OtherPath);
        workspace.ActiveSession.HasUnsavedEdits.ShouldBeFalse();
    }

    [Fact]
    public async Task Close_RemovesTab_AndStopsWatching()
    {
        _store.Seed(Path, "# Loaded");
        _picker.OpenResult = Path;
        var workspace = CreateWorkspace();
        await workspace.OpenAsync();
        var opened = workspace.ActiveSession!;
        var openedWatcher = _watchers[1]; // [0] is the initial empty tab's watcher
        openedWatcher.WatchedPath.ShouldBe(Path);

        await workspace.CloseSessionAsync(opened);

        workspace.Sessions.ShouldNotContain(opened);
        openedWatcher.WatchedPath.ShouldBeNull();
    }

    [Fact]
    public async Task Close_LastTab_LeavesWorkspaceEmpty_INV008()
    {
        var workspace = CreateWorkspace();
        var only = workspace.ActiveSession;

        await workspace.CloseSessionAsync(only);

        // Closing the last Tab empties the Workspace — it does not re-seed a fresh Tab (INV-008).
        workspace.Sessions.ShouldBeEmpty();
        workspace.ActiveSession.ShouldBeNull();
        workspace.HasOpenSessions.ShouldBeFalse();
    }

    [Fact]
    public void New_FromEmptyWorkspace_OpensATabAndActivatesIt_INV008()
    {
        var workspace = CreateWorkspace();
        workspace.CloseSessionCommand.Execute(workspace.ActiveSession); // empty the Workspace

        workspace.New();

        workspace.Sessions.Count.ShouldBe(1);
        workspace.ActiveSession.ShouldBe(workspace.Sessions[0]);
        workspace.HasOpenSessions.ShouldBeTrue();
    }

    [Fact]
    public async Task Close_WithUnsavedEdits_Save_Persists_INV010()
    {
        _store.Seed(Path, "old");
        _picker.OpenResult = Path;
        var workspace = CreateWorkspace();
        await workspace.OpenAsync();
        var opened = workspace.ActiveSession!;
        opened.Markdown = "# Edited";
        _prompt.Decision = UnsavedEditsDecision.Save;

        await workspace.CloseSessionAsync(opened);

        _prompt.ConfirmCount.ShouldBe(1);
        _store.SavedText(Path).ShouldBe("# Edited");
        workspace.Sessions.ShouldNotContain(opened);
    }

    [Fact]
    public async Task Close_WithUnsavedEdits_Discard_Closes_INV010()
    {
        _store.Seed(Path, "old");
        _picker.OpenResult = Path;
        var workspace = CreateWorkspace();
        await workspace.OpenAsync();
        var opened = workspace.ActiveSession!;
        opened.Markdown = "# Edited but discarded";
        _prompt.Decision = UnsavedEditsDecision.Discard;

        await workspace.CloseSessionAsync(opened);

        _prompt.ConfirmCount.ShouldBe(1);
        _store.SavedText(Path).ShouldBe("old"); // never persisted
        workspace.Sessions.ShouldNotContain(opened);
    }

    [Fact]
    public async Task Close_WithUnsavedEdits_Cancel_KeepsTab_INV010()
    {
        _store.Seed(Path, "old");
        _picker.OpenResult = Path;
        var workspace = CreateWorkspace();
        await workspace.OpenAsync();
        var opened = workspace.ActiveSession!;
        opened.Markdown = "# Edited";
        _prompt.Decision = UnsavedEditsDecision.Cancel;

        await workspace.CloseSessionAsync(opened);

        _prompt.ConfirmCount.ShouldBe(1);
        workspace.Sessions.ShouldContain(opened);
        workspace.ActiveSession.ShouldBe(opened);
        opened.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public async Task Close_BackgroundTab_LeavesActiveSessionUnchanged()
    {
        _store.Seed(Path, "# Loaded");
        _picker.OpenResult = Path;
        var workspace = CreateWorkspace();
        var initialEmpty = workspace.ActiveSession;
        await workspace.OpenAsync();
        var opened = workspace.ActiveSession!;

        await workspace.CloseSessionAsync(initialEmpty);

        workspace.Sessions.ShouldNotContain(initialEmpty);
        workspace.ActiveSession.ShouldBe(opened);
    }
}
