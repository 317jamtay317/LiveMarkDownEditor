using Infrastructure.Markdown;
using Shouldly;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for how the <see cref="WorkspaceViewModel"/> takes part in Rename File: a renamed File's Tab
/// follows it to its New Name with its unsaved edits kept, the Recent Files follow it too, and a New
/// Name another Tab already holds is refused so no file is open in two Tabs (INV-082, INV-009).
/// </summary>
public sealed class WorkspaceViewModelRenameFileTests
{
    private const string Vault = @"C:\vault";
    private const string OtherPath = @"C:\docs\other.md";

    private readonly FakeDocumentStore _store = new();
    private readonly StubFilePicker _picker = new();
    private readonly StubUnsavedEditsPrompt _unsavedPrompt = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly FakeWorkspaceStateStore _stateStore = new();
    private readonly StubFolderPicker _folderPicker = new() { FolderResult = Vault };
    private readonly FakeMarkdownFolderReader _folderReader = new() { Result = ["note.md"] };
    private readonly FakeFolderWatcher _folderWatcher = new();
    private readonly FakeFileRenamer _renamer = new();
    private readonly StubRenameFileNotice _notice = new();
    private readonly Dictionary<EditorSessionViewModel, FakeDocumentWatcher> _watchers = [];

    private static string NotePath => System.IO.Path.GetFullPath(@"C:\vault\note.md");

    private static string IdeasPath => System.IO.Path.GetFullPath(@"C:\vault\ideas.md");

    [Fact]
    public async Task RenamingAnOpenFile_ItsTabFollowsIt_INV082()
    {
        var workspace = await CreateWithNoteOpenAsync();
        var opened = workspace.ActiveSession!;
        var tabs = workspace.Sessions.ToList();

        await workspace.Folder.RenameAsync(workspace.Folder.Folder!.Entries[0], "ideas");

        workspace.Sessions.ShouldBe(tabs);
        workspace.ActiveSession.ShouldBe(opened);
        opened.FilePath.ShouldBe(IdeasPath);
        opened.Name.ShouldBe("ideas.md");
    }

    [Fact]
    public async Task RenamingAnOpenFile_WithUnsavedEdits_KeepsThem_AskingAndSavingNothing_INV082()
    {
        var workspace = await CreateWithNoteOpenAsync();
        var opened = workspace.ActiveSession!;
        opened.Markdown = "# Edited";

        await workspace.Folder.RenameAsync(workspace.Folder.Folder!.Entries[0], "ideas");

        opened.Markdown.ShouldBe("# Edited");
        opened.HasUnsavedEdits.ShouldBeTrue();
        _unsavedPrompt.ConfirmCount.ShouldBe(0);
        _store.SavedText(IdeasPath).ShouldBeNull();
    }

    [Fact]
    public async Task RenamingAnOpenFile_WatchesItAtItsNewPath_INV082()
    {
        var workspace = await CreateWithNoteOpenAsync();

        await workspace.Folder.RenameAsync(workspace.Folder.Folder!.Entries[0], "ideas");

        _watchers[workspace.ActiveSession!].WatchedPath.ShouldBe(IdeasPath);
    }

    [Fact]
    public async Task RenamingAFile_RenamesItInTheRecentFiles_AndPersistsTheNewPath_INV082()
    {
        var workspace = await CreateWithNoteOpenAsync();

        await workspace.Folder.RenameAsync(workspace.Folder.Folder!.Entries[0], "ideas");

        workspace.RecentFiles.ShouldContain(IdeasPath);
        workspace.RecentFiles.ShouldNotContain(NotePath);
        _stateStore.SavedState!.RecentFiles.ShouldContain(IdeasPath);
        _stateStore.SavedState.OpenDocuments.ShouldBe([IdeasPath]);
    }

    [Fact]
    public async Task RenamingAFileThatIsNotOpen_LeavesTheOpenTabsAlone_INV082()
    {
        _store.Seed(OtherPath, "# Other");
        _picker.OpenResult = OtherPath;
        var workspace = Create();
        await workspace.Folder.OpenFolderAsync();
        await workspace.OpenAsync();
        var other = workspace.ActiveSession!;
        var tabs = workspace.Sessions.ToList();

        await workspace.Folder.RenameAsync(workspace.Folder.Folder!.Entries[0], "ideas");

        workspace.Sessions.ShouldBe(tabs);
        other.FilePath.ShouldBe(OtherPath);
        _renamer.Renamed.ShouldBe([(NotePath, IdeasPath)]);
    }

    [Fact]
    public async Task RenamingToANameAnotherTabHolds_IsRefused_AndLeavesBothTabs_INV082()
    {
        // ideas.md was deleted outside the editor while its Tab stayed open, so the Folder Tree no
        // longer shows it; renaming note.md to it would open one file in two Tabs (INV-009).
        _store.Seed(IdeasPath, "# Ideas");
        _picker.OpenResult = IdeasPath;
        var workspace = await CreateWithNoteOpenAsync();
        await workspace.OpenAsync();
        var note = workspace.Sessions.Single(session => session.FilePath == NotePath);
        var tabs = workspace.Sessions.ToList();

        await workspace.Folder.RenameAsync(workspace.Folder.Folder!.Entries[0], "ideas");

        _renamer.Renamed.ShouldBeEmpty();
        _notice.Reasons.Single().ShouldContain("ideas.md");
        note.FilePath.ShouldBe(NotePath);
        workspace.Sessions.ShouldBe(tabs);
    }

    private async Task<WorkspaceViewModel> CreateWithNoteOpenAsync()
    {
        _store.Seed(NotePath, "# Note");
        var workspace = Create();
        await workspace.Folder.OpenFolderAsync();
        await workspace.Folder.ActivateAsync(workspace.Folder.Folder!.Entries[0]);
        workspace.ActiveSession!.FilePath.ShouldBe(NotePath);
        return workspace;
    }

    private WorkspaceViewModel Create()
    {
        EditorSessionFactory factory = () =>
        {
            var watcher = new FakeDocumentWatcher();
            var session = new EditorSessionViewModel(_store, watcher, _dispatcher, new FakeMarkdownRoundTrip());
            _watchers[session] = watcher;
            return session;
        };
        var folder = new FolderWorkspaceViewModel(
            _folderPicker, _folderReader, _folderWatcher, _dispatcher, new StubDeleteFilePrompt(), new FakeFileDeleter(), _renamer, _notice);
        return new WorkspaceViewModel(
            factory,
            _picker,
            _unsavedPrompt,
            new StubLinkPrompt(answer: null),
            new FakeDocumentPrinter(),
            new StubMarkdownRenderer(),
            new StubDiagramBuilder(result: null),
            new FakeMermaidImageRenderer(),
            new ColorCodeSyntaxHighlighter(),
            new AppearanceViewModel(new FakeThemeService()),
            new ExportViewModel(
                _picker,
                new StubMarkdownRenderer(),
                new FakeHtmlExportStore(),
                new FakePdfExporter(),
                new FakePdfExportStore(),
                new FakeMermaidScriptSource()),
            folder,
            new SideDockViewModel(folder),
            _stateStore,
            new FakePageSetupStore(),
            new StubCustomMarginsPrompt(answer: null),
            new FakePrintPreview());
    }
}
