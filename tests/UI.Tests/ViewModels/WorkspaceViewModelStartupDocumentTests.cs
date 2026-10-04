using System.IO;
using Infrastructure.Markdown;
using Shouldly;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for how opening a Startup Document brings the Folder Panel to where the file lives (INV-089):
/// its folder becomes the Folder Workspace unless the open Folder Tree already holds it, the panel is
/// shown and highlights the file, and a document that fails to open changes no folder.
/// </summary>
public sealed class WorkspaceViewModelStartupDocumentTests
{
    private const string Vault = @"C:\vault";

    private readonly FakeDocumentStore _store = new();
    private readonly StubFilePicker _picker = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly FakeWorkspaceStateStore _stateStore = new();
    private readonly StubFolderPicker _folderPicker = new() { FolderResult = Vault };
    private readonly FakeMarkdownFolderReader _folderReader = new() { Result = ["readme.md", "sub/second.md"] };
    private readonly FakeFolderWatcher _folderWatcher = new();

    private static string NotesFolder => Path.GetFullPath(@"C:\docs\notes");

    private static string StartupPath => Path.GetFullPath(@"C:\docs\notes\readme.md");

    private static string VaultSecondPath => Path.GetFullPath(@"C:\vault\sub\second.md");

    [Fact]
    public async Task OpenStartupDocument_WithNoFolderOpen_OpensItsFolder_INV089()
    {
        _store.Seed(StartupPath, "# Readme");
        var workspace = Create();

        await workspace.OpenStartupDocumentAsync(StartupPath);

        workspace.ActiveSession!.FilePath.ShouldBe(StartupPath);
        workspace.Folder.Folder!.RootPath.ShouldBe(NotesFolder);
        workspace.Folder.IsFolderPanelVisible.ShouldBeTrue();
        workspace.Folder.SelectedEntry?.RelativePath.ShouldBe("readme.md");
        _folderWatcher.WatchedRoot.ShouldBe(NotesFolder);
    }

    [Fact]
    public async Task OpenStartupDocument_OutsideTheOpenFolder_OpensItsFolderInstead_INV089()
    {
        _store.Seed(StartupPath, "# Readme");
        var workspace = Create();
        await workspace.Folder.OpenFolderAsync();

        await workspace.OpenStartupDocumentAsync(StartupPath);

        workspace.Folder.Folder!.RootPath.ShouldBe(NotesFolder);
        workspace.Folder.SelectedEntry?.RelativePath.ShouldBe("readme.md");
    }

    [Fact]
    public async Task OpenStartupDocument_PersistsTheNewRoot_INV089()
    {
        _store.Seed(StartupPath, "# Readme");
        var workspace = Create();

        await workspace.OpenStartupDocumentAsync(StartupPath);

        _stateStore.SavedState!.WorkspaceFolder.ShouldBe(NotesFolder);
        _stateStore.SavedState.OpenDocuments.ShouldContain(StartupPath);
    }

    [Fact]
    public async Task OpenStartupDocument_HeldByTheOpenFolder_KeepsTheRoot_INV089()
    {
        _store.Seed(VaultSecondPath, "# Second");
        var workspace = Create();
        await workspace.Folder.OpenFolderAsync();

        await workspace.OpenStartupDocumentAsync(VaultSecondPath);

        workspace.Folder.Folder!.RootPath.ShouldBe(Vault);
        workspace.Folder.SelectedEntry?.RelativePath.ShouldBe("sub/second.md");
    }

    [Fact]
    public async Task OpenStartupDocument_HeldByTheOpenFolder_ShowsAHiddenFolderPanel_INV089()
    {
        _store.Seed(VaultSecondPath, "# Second");
        var workspace = Create();
        await workspace.Folder.OpenFolderAsync();
        workspace.Folder.CloseFolderPanel();

        await workspace.OpenStartupDocumentAsync(VaultSecondPath);

        workspace.Folder.IsFolderPanelVisible.ShouldBeTrue();
    }

    [Fact]
    public async Task OpenStartupDocument_WithTheOutlineTabSelected_SelectsTheFolderTab_INV089()
    {
        _store.Seed(StartupPath, "# Readme");
        var workspace = Create();
        await workspace.Folder.OpenFolderAsync();
        workspace.SideDock.OpenNavigationPanel();
        workspace.SideDock.SelectedTab.ShouldBe(SideDockTab.Navigation);

        await workspace.OpenStartupDocumentAsync(StartupPath);

        workspace.SideDock.SelectedTab.ShouldBe(SideDockTab.Folder);
    }

    [Fact]
    public async Task OpenStartupDocument_HeldByTheOpenFolder_SelectsTheFolderTab_INV089()
    {
        _store.Seed(VaultSecondPath, "# Second");
        var workspace = Create();
        await workspace.Folder.OpenFolderAsync();
        workspace.SideDock.OpenNavigationPanel();

        await workspace.OpenStartupDocumentAsync(VaultSecondPath);

        workspace.SideDock.SelectedTab.ShouldBe(SideDockTab.Folder);
    }

    [Fact]
    public async Task OpenStartupDocument_ThatFailsToOpen_ChangesNoFolder_INV089()
    {
        var workspace = Create();
        await workspace.Folder.OpenFolderAsync();

        await Should.ThrowAsync<IOException>(() => workspace.OpenStartupDocumentAsync(StartupPath));

        workspace.Folder.Folder!.RootPath.ShouldBe(Vault);
    }

    [Fact]
    public async Task OpenStartupDocument_WhoseFolderCannotBeRead_KeepsTheOpenFolder_INV089()
    {
        _store.Seed(StartupPath, "# Readme");
        _folderReader.MissingRoots.Add(NotesFolder);
        var workspace = Create();
        await workspace.Folder.OpenFolderAsync();

        await workspace.OpenStartupDocumentAsync(StartupPath);

        workspace.ActiveSession!.FilePath.ShouldBe(StartupPath);
        workspace.Folder.Folder!.RootPath.ShouldBe(Vault);
    }

    [Fact]
    public async Task OpenPath_OutsideTheOpenFolder_KeepsTheRoot_INV089()
    {
        _store.Seed(StartupPath, "# Readme");
        var workspace = Create();
        await workspace.Folder.OpenFolderAsync();

        // Only a Startup Document moves the Folder Workspace; opening from inside the editor does not.
        await workspace.OpenPathAsync(StartupPath);

        workspace.Folder.Folder!.RootPath.ShouldBe(Vault);
    }

    private WorkspaceViewModel Create()
    {
        EditorSessionFactory factory = () =>
            new EditorSessionViewModel(_store, new FakeDocumentWatcher(), _dispatcher, new FakeMarkdownRoundTrip());
        var folder = new FolderWorkspaceViewModel(
            _folderPicker, _folderReader, _folderWatcher, _dispatcher, new StubDeleteFilePrompt(), new FakeFileDeleter(), new FakeFileRenamer(), new StubRenameFileNotice(), new FakeEntryCreator(), new StubNewEntryNotice());
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
            _stateStore,
            new FakePageSetupStore(),
            new StubCustomMarginsPrompt(answer: null),
            new FakePrintPreview());
    }
}
