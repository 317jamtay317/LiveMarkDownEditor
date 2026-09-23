using Infrastructure.Markdown;
using Shouldly;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for how the <see cref="WorkspaceViewModel"/> drives the Folder Panel's highlight (INV-083):
/// changing the Active Session — by selecting a Tab, by opening a document, or by closing every Tab —
/// makes the Folder Tree show the File being edited, and leaves nothing highlighted when the Active
/// Session is not a File the tree holds.
/// </summary>
public sealed class WorkspaceViewModelFollowActiveSessionTests
{
    private const string Vault = @"C:\vault";
    private const string OutsidePath = @"C:\docs\outside.md";

    private readonly FakeDocumentStore _store = new();
    private readonly StubFilePicker _picker = new();
    private readonly StubUnsavedEditsPrompt _unsavedPrompt = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly FakeWorkspaceStateStore _stateStore = new();
    private readonly StubFolderPicker _folderPicker = new() { FolderResult = Vault };
    private readonly FakeMarkdownFolderReader _folderReader = new() { Result = ["first.md", "sub/second.md"] };
    private readonly FakeFolderWatcher _folderWatcher = new();

    private static string FirstPath => System.IO.Path.GetFullPath(@"C:\vault\first.md");

    private static string SecondPath => System.IO.Path.GetFullPath(@"C:\vault\sub\second.md");

    [Fact]
    public async Task OpeningAFile_HighlightsItInTheFolderPanel_INV083()
    {
        var workspace = await CreateWithFolderAsync();

        await workspace.OpenPathAsync(SecondPath);

        workspace.Folder.SelectedEntry?.RelativePath.ShouldBe("sub/second.md");
    }

    [Fact]
    public async Task ChangingTab_HighlightsTheNewTabsFile_INV083()
    {
        var workspace = await CreateWithFolderAsync();
        await workspace.OpenPathAsync(FirstPath);
        var first = workspace.ActiveSession!;
        await workspace.OpenPathAsync(SecondPath);
        workspace.Folder.SelectedEntry?.RelativePath.ShouldBe("sub/second.md");

        workspace.ActiveSession = first;

        workspace.Folder.SelectedEntry?.RelativePath.ShouldBe("first.md");
    }

    [Fact]
    public async Task ChangingToATabOutsideTheFolder_HighlightsNothing_INV083()
    {
        _store.Seed(OutsidePath, "# Outside");
        var workspace = await CreateWithFolderAsync();
        await workspace.OpenPathAsync(FirstPath);
        var inside = workspace.ActiveSession!;
        await workspace.OpenPathAsync(OutsidePath);
        var outside = workspace.ActiveSession!;

        workspace.ActiveSession = inside;
        workspace.ActiveSession = outside;

        workspace.Folder.SelectedEntry.ShouldBeNull();
    }

    [Fact]
    public async Task ChangingToAnUnsavedTab_HighlightsNothing_INV083()
    {
        var workspace = await CreateWithFolderAsync();
        await workspace.OpenPathAsync(FirstPath);
        workspace.Folder.SelectedEntry.ShouldNotBeNull();

        workspace.New();

        workspace.ActiveSession!.FilePath.ShouldBeNull();
        workspace.Folder.SelectedEntry.ShouldBeNull();
    }

    [Fact]
    public async Task ClosingEveryTab_HighlightsNothing_INV083()
    {
        var workspace = await CreateWithFolderAsync();
        await workspace.OpenPathAsync(FirstPath);
        workspace.Folder.SelectedEntry.ShouldNotBeNull();

        await workspace.CloseAllTabsAsync();

        workspace.ActiveSession.ShouldBeNull();
        workspace.Folder.SelectedEntry.ShouldBeNull();
    }

    [Fact]
    public async Task ActivatingAFileInTheFolderPanel_HighlightsIt_INV083()
    {
        var workspace = await CreateWithFolderAsync();
        var sub = workspace.Folder.Folder!.Entries[0];

        await workspace.Folder.ActivateAsync(sub.Children[0]);

        // Opening a File from the panel makes its Tab the Active Session, which the highlight follows.
        workspace.ActiveSession!.FilePath.ShouldBe(SecondPath);
        workspace.Folder.SelectedEntry?.RelativePath.ShouldBe("sub/second.md");
    }

    private async Task<WorkspaceViewModel> CreateWithFolderAsync()
    {
        _store.Seed(FirstPath, "# First");
        _store.Seed(SecondPath, "# Second");
        var workspace = Create();
        await workspace.Folder.OpenFolderAsync();
        return workspace;
    }

    private WorkspaceViewModel Create()
    {
        EditorSessionFactory factory = () =>
            new EditorSessionViewModel(_store, new FakeDocumentWatcher(), _dispatcher, new FakeMarkdownRoundTrip());
        var folder = new FolderWorkspaceViewModel(
            _folderPicker, _folderReader, _folderWatcher, _dispatcher, new StubDeleteFilePrompt(), new FakeFileDeleter(), new FakeFileRenamer(), new StubRenameFileNotice());
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
