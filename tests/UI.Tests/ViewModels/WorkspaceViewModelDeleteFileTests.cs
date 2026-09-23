using Infrastructure.Markdown;
using Shouldly;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for how the <see cref="WorkspaceViewModel"/> takes part in Delete File: a deleted File's Tab
/// closes through Close Tab, so unsaved edits are asked about (INV-010), and the File leaves the
/// Recent Files (INV-081).
/// </summary>
public sealed class WorkspaceViewModelDeleteFileTests
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
    private readonly StubDeleteFilePrompt _deletePrompt = new();
    private readonly FakeFileDeleter _deleter = new();

    private static string NotePath => System.IO.Path.GetFullPath(@"C:\vault\note.md");

    [Fact]
    public async Task DeletingAnOpenFile_ClosesItsTab_INV081()
    {
        var workspace = await CreateWithNoteOpenAsync();
        var opened = workspace.ActiveSession!;

        await workspace.Folder.DeleteAsync(workspace.Folder.Folder!.Entries[0]);

        workspace.Sessions.ShouldNotContain(opened);
        _deleter.Deleted.ShouldBe([NotePath]);
    }

    [Fact]
    public async Task DeletingAnOpenFile_WithUnsavedEdits_AsksAboutThem_INV081()
    {
        var workspace = await CreateWithNoteOpenAsync();
        workspace.ActiveSession!.Markdown = "# Edited";

        await workspace.Folder.DeleteAsync(workspace.Folder.Folder!.Entries[0]);

        _unsavedPrompt.DocumentNames.ShouldBe(["note.md"]);
    }

    [Fact]
    public async Task DeletingAnOpenFile_WithUnsavedEdits_Cancel_KeepsTheTabAndTheFile_INV081()
    {
        var workspace = await CreateWithNoteOpenAsync();
        var opened = workspace.ActiveSession!;
        opened.Markdown = "# Edited";
        _unsavedPrompt.Decision = UnsavedEditsDecision.Cancel;

        await workspace.Folder.DeleteAsync(workspace.Folder.Folder!.Entries[0]);

        workspace.Sessions.ShouldContain(opened);
        opened.Markdown.ShouldBe("# Edited");
        _deleter.Deleted.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeletingAnOpenFile_WithUnsavedEdits_Save_WritesThemBeforeTheFileIsRecycled_INV081()
    {
        var workspace = await CreateWithNoteOpenAsync();
        workspace.ActiveSession!.Markdown = "# Edited";
        _unsavedPrompt.Decision = UnsavedEditsDecision.Save;
        string? textWhenDeleted = null;
        _deleter.OnDelete = path => textWhenDeleted = _store.SavedText(path);

        await workspace.Folder.DeleteAsync(workspace.Folder.Folder!.Entries[0]);

        // The copy in the Recycle Bin is the one with the user's edits in it.
        textWhenDeleted.ShouldBe("# Edited");
    }

    [Fact]
    public async Task DeletingAFile_DropsItFromTheRecentFiles_INV081()
    {
        var workspace = await CreateWithNoteOpenAsync();
        workspace.RecentFiles.ShouldContain(NotePath);

        await workspace.Folder.DeleteAsync(workspace.Folder.Folder!.Entries[0]);

        workspace.RecentFiles.ShouldNotContain(NotePath);
        _stateStore.SavedState!.RecentFiles.ShouldNotContain(NotePath);
    }

    [Fact]
    public async Task DeletingAFileThatIsNotOpen_LeavesTheOpenTabsAlone_INV081()
    {
        _store.Seed(OtherPath, "# Other");
        _picker.OpenResult = OtherPath;
        var workspace = Create();
        await workspace.Folder.OpenFolderAsync();
        await workspace.OpenAsync();
        var other = workspace.ActiveSession!;
        other.Markdown = "# Other, edited";

        await workspace.Folder.DeleteAsync(workspace.Folder.Folder!.Entries[0]);

        workspace.Sessions.ShouldContain(other);
        _unsavedPrompt.ConfirmCount.ShouldBe(0);
        _deleter.Deleted.ShouldBe([NotePath]);
    }

    [Fact]
    public async Task DecliningTheDelete_KeepsTheTabOpen_INV081()
    {
        var workspace = await CreateWithNoteOpenAsync();
        var opened = workspace.ActiveSession!;
        _deletePrompt.Answer = false;

        await workspace.Folder.DeleteAsync(workspace.Folder.Folder!.Entries[0]);

        workspace.Sessions.ShouldContain(opened);
        workspace.RecentFiles.ShouldContain(NotePath);
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
            new EditorSessionViewModel(_store, new FakeDocumentWatcher(), _dispatcher, new FakeMarkdownRoundTrip());
        var folder = new FolderWorkspaceViewModel(
            _folderPicker, _folderReader, _folderWatcher, _dispatcher, _deletePrompt, _deleter, new FakeFileRenamer(), new StubRenameFileNotice());
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
