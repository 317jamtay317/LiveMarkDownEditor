using Application;
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
    private readonly FakeWorkspaceStateStore _stateStore = new();
    private readonly StubFolderPicker _folderPicker = new();
    private readonly FakeMarkdownFolderReader _folderReader = new();
    private readonly FakeFolderWatcher _folderWatcher = new();
    private readonly List<FakeDocumentWatcher> _watchers = [];

    private WorkspaceViewModel CreateWorkspace()
    {
        EditorSessionFactory factory = () =>
        {
            var watcher = new FakeDocumentWatcher();
            _watchers.Add(watcher);
            return new EditorSessionViewModel(_store, watcher, _dispatcher, _roundTrip);
        };
        var folder = new FolderWorkspaceViewModel(_folderPicker, _folderReader, _folderWatcher, _dispatcher);
        return new WorkspaceViewModel(
            factory,
            _picker,
            _prompt,
            new StubLinkPrompt(answer: null),
            new FakeDocumentPrinter(),
            new StubMarkdownRenderer(),
            new StubFlowchartBuilder(result: null),
            new FakeMermaidImageRenderer(),
            new AppearanceViewModel(_theme),
            new ExportViewModel(
                _picker,
                new StubMarkdownRenderer(),
                new FakeHtmlExportStore(),
                new FakePdfExporter(),
                new FakePdfExportStore(),
                new FakeMermaidScriptSource()),
            folder,
            new SideDockViewModel(folder),
            _stateStore);
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
    public void Constructor_StartsWithPreviewPanelHidden_INV048()
    {
        var workspace = CreateWorkspace();

        // The Preview Panel is hidden until the user toggles it on.
        workspace.IsPreviewPanelVisible.ShouldBeFalse();
    }

    [Fact]
    public void TogglePreviewPanel_TogglesVisibility_WithoutChangingDocument_INV048()
    {
        var workspace = CreateWorkspace();
        workspace.ActiveSession!.Markdown = "```mermaid\ngraph TD\n  A-->B\n```";
        var sourceBefore = workspace.ActiveSession.Markdown;

        workspace.TogglePreviewPanelCommand.Execute(null);
        workspace.IsPreviewPanelVisible.ShouldBeTrue();

        workspace.TogglePreviewPanelCommand.Execute(null);
        workspace.IsPreviewPanelVisible.ShouldBeFalse();

        // Toggling the Preview Panel is view-only: the Markdown Document is untouched (INV-048).
        workspace.ActiveSession.Markdown.ShouldBe(sourceBefore);
    }

    [Fact]
    public void NarrowWorkspaceWidth_HidesTheToggledSourceAndPreviewPanels_INV059()
    {
        var workspace = CreateWorkspace();
        workspace.ToggleSourcePanelCommand.Execute(null);
        workspace.TogglePreviewPanelCommand.Execute(null);

        // Wide enough for both beside the editor: both shown as toggled.
        workspace.WorkspaceWidth = 1400;
        workspace.IsSourcePanelVisible.ShouldBeTrue();
        workspace.IsPreviewPanelVisible.ShouldBeTrue();

        // Too narrow for either: Compact Layout auto-collapses both so the editor keeps its width (INV-059).
        workspace.WorkspaceWidth = 300;
        workspace.IsSourcePanelVisible.ShouldBeFalse();
        workspace.IsPreviewPanelVisible.ShouldBeFalse();
    }

    [Fact]
    public void WideningTheWorkspaceAgain_RestoresThePanels_AsTheyWereToggled_INV059()
    {
        var workspace = CreateWorkspace();
        workspace.ActiveSession!.Markdown = "# Title";
        var sourceBefore = workspace.ActiveSession.Markdown;
        workspace.ToggleSourcePanelCommand.Execute(null);

        workspace.WorkspaceWidth = 300;   // auto-collapsed while narrow
        workspace.IsSourcePanelVisible.ShouldBeFalse();

        workspace.WorkspaceWidth = 1400;  // room again → restored to the user's toggle intent
        workspace.IsSourcePanelVisible.ShouldBeTrue();

        // Compact Layout is view-only: the Markdown Document is untouched (INV-059).
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

    [Fact]
    public async Task RestoreAsync_ReopensTheSavedTabs_INV037()
    {
        _store.Seed(Path, "# One");
        _store.Seed(OtherPath, "# Two");
        _stateStore.StateToLoad = new WorkspaceState([Path, OtherPath], []);
        var workspace = CreateWorkspace();

        await workspace.RestoreAsync();

        // The placeholder empty Tab is replaced by the restored Tabs.
        workspace.Sessions.Select(session => session.FilePath).ShouldBe([Path, OtherPath]);
    }

    [Fact]
    public async Task RestoreAsync_SkipsFilesThatHaveGone_INV037()
    {
        _store.Seed(Path, "# One"); // OtherPath is not seeded, so it "has gone".
        _stateStore.StateToLoad = new WorkspaceState([Path, OtherPath], []);
        var workspace = CreateWorkspace();

        await workspace.RestoreAsync();

        workspace.Sessions.Select(session => session.FilePath).ShouldBe([Path]);
    }

    [Fact]
    public async Task RestoreAsync_WithNothingToRestore_KeepsTheEmptyTab_INV008_INV037()
    {
        var workspace = CreateWorkspace(); // the store loads WorkspaceState.Empty

        await workspace.RestoreAsync();

        workspace.Sessions.Count.ShouldBe(1);
        workspace.ActiveSession!.FilePath.ShouldBeNull();
    }

    [Fact]
    public async Task RestoreAsync_LoadsTheRecentFiles_INV037()
    {
        _stateStore.StateToLoad = new WorkspaceState([], [Path, OtherPath]);
        var workspace = CreateWorkspace();

        await workspace.RestoreAsync();

        workspace.RecentFiles.ShouldBe([Path, OtherPath]);
    }

    [Fact]
    public async Task Open_AddsTheFileToRecentFiles_INV037()
    {
        _store.Seed(Path, "# Loaded");
        _picker.OpenResult = Path;
        var workspace = CreateWorkspace();

        await workspace.OpenAsync();

        workspace.RecentFiles.ShouldContain(Path);
    }

    [Fact]
    public async Task Save_AddsTheFileToRecentFiles_INV037()
    {
        _picker.SaveResult = OtherPath;
        var workspace = CreateWorkspace();
        workspace.ActiveSession!.Markdown = "# New";

        await workspace.SaveActiveAsync();

        workspace.RecentFiles.ShouldContain(OtherPath);
    }

    [Fact]
    public async Task Open_PersistsOnlySavedTabs_INV037()
    {
        _store.Seed(Path, "# Loaded");
        _picker.OpenResult = Path;
        var workspace = CreateWorkspace(); // starts with one empty (unsaved) Tab

        await workspace.OpenAsync();

        // The empty Tab has no Watched File, so only the opened file is persisted.
        _stateStore.SavedState!.OpenDocuments.ShouldBe([Path]);
    }

    [Fact]
    public async Task OpenFolder_PersistsTheFolderRootToTheStateStore_INV045()
    {
        _folderPicker.FolderResult = @"C:\vault";
        _folderReader.Result = ["a.md"];
        var workspace = CreateWorkspace();

        await workspace.Folder.OpenFolderAsync();

        _stateStore.SavedState!.WorkspaceFolder.ShouldBe(@"C:\vault");
    }

    [Fact]
    public async Task RestoreAsync_ReopensThePersistedFolder_INV045()
    {
        _stateStore.StateToLoad = new WorkspaceState([], [], WorkspaceFolder: @"C:\vault");
        _folderReader.Result = ["a.md"];
        var workspace = CreateWorkspace();

        await workspace.RestoreAsync();

        workspace.Folder.Folder.ShouldNotBeNull();
        workspace.Folder.Folder!.RootPath.ShouldBe(@"C:\vault");
    }

    [Fact]
    public async Task RestoreAsync_WhenThePersistedFolderHasGone_OpensNoFolder_INV045()
    {
        _stateStore.StateToLoad = new WorkspaceState([], [], WorkspaceFolder: @"C:\gone");
        _folderReader.MissingRoots.Add(@"C:\gone");
        var workspace = CreateWorkspace();

        await workspace.RestoreAsync();

        workspace.Folder.Folder.ShouldBeNull();
    }
}
