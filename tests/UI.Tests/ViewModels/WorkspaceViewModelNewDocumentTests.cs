using Infrastructure.Markdown;
using Shouldly;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for New Document on the <see cref="WorkspaceViewModel"/> — it always makes a file: New File in
/// place with a Folder Entry selected, otherwise through the save prompt (INV-087) — and for Save As,
/// which saves the Active Session to a file the user picks (INV-088).
/// </summary>
public sealed class WorkspaceViewModelNewDocumentTests
{
    private const string Vault = @"C:\vault";
    private const string NotePath = @"C:\docs\note.md";

    private readonly FakeDocumentStore _store = new();
    private readonly StubFilePicker _picker = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly StubFolderPicker _folderPicker = new() { FolderResult = Vault };
    private readonly FakeMarkdownFolderReader _folderReader = new();

    private static string VaultPath(string relative) => System.IO.Path.GetFullPath(System.IO.Path.Combine(Vault, relative));

    [Fact]
    public async Task NewDocument_WithAFolderSelected_AsksTheFolderPanelForNewFile_AndOpensNoTab_INV087()
    {
        var workspace = await OpenVaultAsync("sub/note.md");
        workspace.Folder.SelectedEntry = workspace.Folder.Folder!.Entries[0];
        var tabs = workspace.Sessions.Count;

        await workspace.NewDocumentAsync();

        workspace.Folder.NewFileRequest.ShouldNotBeNull();
        workspace.Folder.IsFolderPanelVisible.ShouldBeTrue();
        workspace.SideDock.SelectedTab.ShouldBe(SideDockTab.Folder);
        workspace.Sessions.Count.ShouldBe(tabs);
        _picker.SaveFolder.ShouldBeNull(); // no prompt
    }

    [Fact]
    public async Task NewDocument_WithAFileSelected_AlsoAsksForNewFile_BesideIt_INV087()
    {
        var workspace = await OpenVaultAsync("top.md");
        workspace.Folder.SelectedEntry = workspace.Folder.Folder!.Entries[0];

        await workspace.NewDocumentAsync();

        workspace.Folder.NewFileRequest.ShouldNotBeNull();
    }

    [Fact]
    public async Task NewDocument_WithNothingSelected_PromptsInTheOpenFolder_AndOpensTheCreatedFile_INV087()
    {
        var workspace = await OpenVaultAsync("top.md");
        workspace.Folder.SelectedEntry = null;
        _picker.SaveResult = VaultPath("ideas.md");

        await workspace.NewDocumentAsync();

        _picker.SaveFolder.ShouldBe(System.IO.Path.GetFullPath(Vault));
        _picker.SuggestedSaveName.ShouldBe("Untitled.md");
        _store.SavedText(VaultPath("ideas.md")).ShouldBe(string.Empty);
        workspace.ActiveSession!.FilePath.ShouldBe(VaultPath("ideas.md"));
        workspace.ActiveSession.HasUnsavedEdits.ShouldBeFalse();
        workspace.Folder.NewFileRequest.ShouldBeNull();
    }

    [Fact]
    public async Task NewDocument_ThroughThePrompt_AsksForTheEditor_SoTheUserCanTypeAtOnce_INV087()
    {
        var workspace = CreateWorkspace();
        _picker.SaveResult = NotePath;

        await workspace.NewDocumentAsync();

        workspace.EditorFocusRequest.ShouldNotBeNull();
    }

    [Fact]
    public async Task NewDocument_WhenThePromptIsCancelled_AsksForNothing_INV087()
    {
        var workspace = CreateWorkspace();
        _picker.SaveResult = null;

        await workspace.NewDocumentAsync();

        workspace.EditorFocusRequest.ShouldBeNull();
    }

    [Fact]
    public async Task NewFile_FromTheFolderPanel_AsksTheWorkspaceForTheEditor_INV086()
    {
        var workspace = await OpenVaultAsync("top.md");
        _store.Seed(VaultPath("ideas.md"), string.Empty); // the empty file New File puts on disk

        await workspace.Folder.NewFileAsync(null, "ideas");

        workspace.ActiveSession!.FilePath.ShouldBe(VaultPath("ideas.md"));
        workspace.EditorFocusRequest.ShouldNotBeNull();
    }

    [Fact]
    public async Task NewDocument_SavedIntoTheOpenFolder_IsHighlightedInTheTree_INV087_INV083()
    {
        var workspace = await OpenVaultAsync("top.md");
        workspace.Folder.SelectedEntry = null;
        _picker.SaveResult = VaultPath("ideas.md");
        _folderReader.Result = ["ideas.md", "top.md"]; // the disk, once the file has been created

        await workspace.NewDocumentAsync();

        workspace.Folder.SelectedEntry!.RelativePath.ShouldBe("ideas.md");
    }

    [Fact]
    public async Task NewDocument_WithNoFolderWorkspaceOpen_PromptsWithNoFolder_AndOpensTheCreatedFile_INV087()
    {
        var workspace = CreateWorkspace();
        var tabs = workspace.Sessions.Count;
        _picker.SaveResult = NotePath;

        await workspace.NewDocumentAsync();

        _picker.SaveFolder.ShouldBeNull();
        _store.SavedText(NotePath).ShouldBe(string.Empty);
        workspace.Sessions.Count.ShouldBe(tabs + 1);
        workspace.ActiveSession!.FilePath.ShouldBe(NotePath);
    }

    [Fact]
    public async Task NewDocument_WhenThePromptIsCancelled_CreatesAndOpensNothing_INV087()
    {
        var workspace = CreateWorkspace();
        var active = workspace.ActiveSession;
        var tabs = workspace.Sessions.Count;
        _picker.SaveResult = null;

        await workspace.NewDocumentAsync();

        workspace.Sessions.Count.ShouldBe(tabs);
        workspace.ActiveSession.ShouldBeSameAs(active);
    }

    [Fact]
    public async Task NewDocument_ForAFileAnotherTabHolds_ActivatesThatTab_AndOverwritesNothing_INV087()
    {
        _store.Seed(NotePath, "# Kept");
        var workspace = CreateWorkspace();
        await workspace.OpenPathAsync(NotePath);
        var holder = workspace.ActiveSession;
        workspace.ActiveSession = workspace.Sessions[0];
        _picker.SaveResult = NotePath;

        await workspace.NewDocumentAsync();

        workspace.ActiveSession.ShouldBeSameAs(holder);
        _store.SavedText(NotePath).ShouldBe("# Kept");
    }

    [Fact]
    public async Task NewCommand_RunsNewDocument_INV087()
    {
        var workspace = CreateWorkspace();
        _picker.SaveResult = NotePath;

        workspace.NewCommand.Execute(null);
        await Task.Yield();

        workspace.ActiveSession!.FilePath.ShouldBe(NotePath);
    }

    [Fact]
    public async Task SaveAs_ADocumentWithAFile_PromptsInItsOwnFolderWithItsName_INV088()
    {
        _store.Seed(NotePath, "# Note");
        var workspace = CreateWorkspace();
        await workspace.OpenPathAsync(NotePath);
        _picker.SaveResult = null;

        await workspace.SaveAsActiveAsync();

        _picker.SaveFolder.ShouldBe(@"C:\docs");
        _picker.SuggestedSaveName.ShouldBe("note.md");
    }

    [Fact]
    public async Task SaveAs_SavesToThePickedFile_AndTheTabHoldsIt_LeavingTheOldFileAlone_INV088()
    {
        _store.Seed(NotePath, "# Note");
        var workspace = CreateWorkspace();
        await workspace.OpenPathAsync(NotePath);
        workspace.ActiveSession!.Markdown = "# Changed";
        const string copy = @"C:\elsewhere\copy.md";
        _picker.SaveResult = copy;

        await workspace.SaveAsActiveAsync();

        _store.SavedText(copy).ShouldNotBeNull();
        _store.SavedText(copy)!.ShouldContain("Changed");
        _store.SavedText(NotePath).ShouldBe("# Note");
        workspace.ActiveSession.FilePath.ShouldBe(copy);
        workspace.ActiveSession.HasUnsavedEdits.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAs_AnUntitledTab_PromptsInTheSaveFolder_INV088()
    {
        var workspace = await OpenVaultAsync("sub/note.md");
        workspace.ActiveSession = workspace.Sessions[0];
        workspace.Folder.SelectedEntry = workspace.Folder.Folder!.Entries[0];
        _picker.SaveResult = null;

        await workspace.SaveAsActiveAsync();

        _picker.SaveFolder.ShouldBe(VaultPath("sub"));
        _picker.SuggestedSaveName.ShouldBe("Untitled.md");
    }

    [Fact]
    public async Task SaveAs_IntoTheOpenFolder_HighlightsTheNewFile_INV088_INV083()
    {
        var workspace = await OpenVaultAsync("sub/note.md");
        _store.Seed(VaultPath(@"sub\note.md"), "# Note");
        await workspace.OpenPathAsync(VaultPath(@"sub\note.md"));
        workspace.Folder.SelectedEntry!.RelativePath.ShouldBe("sub/note.md");
        _picker.SaveResult = VaultPath("copy.md");
        _folderReader.Result = ["copy.md", "sub/note.md"]; // the disk, once Save As has written the copy

        await workspace.SaveAsActiveAsync();

        workspace.Folder.SelectedEntry!.RelativePath.ShouldBe("copy.md");
        workspace.Folder.SaveFolder.ShouldBe(System.IO.Path.GetFullPath(Vault));
    }

    [Fact]
    public async Task SaveAs_OutsideTheOpenFolder_LeavesNothingHighlighted_INV088_INV083()
    {
        var workspace = await OpenVaultAsync("sub/note.md");
        _store.Seed(VaultPath(@"sub\note.md"), "# Note");
        await workspace.OpenPathAsync(VaultPath(@"sub\note.md"));
        _picker.SaveResult = @"C:\elsewhere\copy.md";

        await workspace.SaveAsActiveAsync();

        workspace.Folder.SelectedEntry.ShouldBeNull();
        workspace.Folder.SaveFolder.ShouldBe(System.IO.Path.GetFullPath(Vault));
    }

    [Fact]
    public async Task Save_AnUntitledTab_IntoTheOpenFolder_HighlightsTheFile_INV080_INV083()
    {
        var workspace = await OpenVaultAsync("top.md");
        workspace.ActiveSession = workspace.Sessions[0]; // the untitled Tab the Workspace starts with
        _picker.SaveResult = VaultPath("ideas.md");
        _folderReader.Result = ["ideas.md", "top.md"];

        await workspace.SaveActiveAsync();

        workspace.Folder.SelectedEntry!.RelativePath.ShouldBe("ideas.md");
    }

    [Fact]
    public async Task SaveAs_WhenCancelled_SavesNothing_INV088()
    {
        _store.Seed(NotePath, "# Note");
        var workspace = CreateWorkspace();
        await workspace.OpenPathAsync(NotePath);
        workspace.ActiveSession!.Markdown = "# Changed";
        _picker.SaveResult = null;

        await workspace.SaveAsActiveAsync();

        workspace.ActiveSession.FilePath.ShouldBe(NotePath);
        workspace.ActiveSession.HasUnsavedEdits.ShouldBeTrue();
        _store.SavedText(NotePath).ShouldBe("# Note");
    }

    [Fact]
    public async Task SaveAs_ToAFileAnotherTabHolds_SavesNothing_AndBringsThatTabForward_INV088()
    {
        const string other = @"C:\docs\other.md";
        _store.Seed(NotePath, "# Note");
        _store.Seed(other, "# Other");
        var workspace = CreateWorkspace();
        await workspace.OpenPathAsync(other);
        var holder = workspace.ActiveSession;
        await workspace.OpenPathAsync(NotePath);
        workspace.ActiveSession!.Markdown = "# Changed";
        _picker.SaveResult = other;

        await workspace.SaveAsActiveAsync();

        workspace.ActiveSession.ShouldBeSameAs(holder);
        _store.SavedText(other).ShouldBe("# Other");
    }

    [Fact]
    public async Task SaveAsCommand_IsAvailableWheneverATabIsOpen_INV088()
    {
        _store.Seed(NotePath, "# Note");
        var workspace = CreateWorkspace();
        await workspace.OpenPathAsync(NotePath);

        workspace.SaveAsCommand.CanExecute(null).ShouldBeTrue(); // even with nothing unsaved
    }

    private async Task<WorkspaceViewModel> OpenVaultAsync(params string[] relativePaths)
    {
        _folderReader.Result = relativePaths;
        var workspace = CreateWorkspace();
        await workspace.Folder.OpenFolderAsync();
        return workspace;
    }

    private WorkspaceViewModel CreateWorkspace()
    {
        EditorSessionFactory factory = () =>
            new EditorSessionViewModel(_store, new FakeDocumentWatcher(), _dispatcher, new FakeMarkdownRoundTrip());
        var folder = new FolderWorkspaceViewModel(
            _folderPicker, _folderReader, new FakeFolderWatcher(), _dispatcher, new StubDeleteFilePrompt(),
            new FakeFileDeleter(), new FakeFileRenamer(), new StubRenameFileNotice(), new FakeEntryCreator(),
            new StubNewEntryNotice());
        return new WorkspaceViewModel(
            factory,
            _picker,
            new StubUnsavedEditsPrompt(),
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
            new FakeWorkspaceStateStore(),
            new FakePageSetupStore(),
            new StubCustomMarginsPrompt(answer: null),
            new FakePrintPreview());
    }
}
