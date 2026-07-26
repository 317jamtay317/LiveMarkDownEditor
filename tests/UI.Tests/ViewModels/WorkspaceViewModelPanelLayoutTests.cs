using Application;
using Infrastructure.Markdown;
using Shouldly;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for the Panel Layout: every Dockable Panel's open and pinned state persisted as part of the
/// Workspace State and restored at startup, so the editor reopens with the panels the user left it
/// with (INV-067).
/// </summary>
public sealed class WorkspaceViewModelPanelLayoutTests
{
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

    private WorkspaceViewModel CreateWorkspace()
    {
        EditorSessionFactory factory = () =>
            new EditorSessionViewModel(_store, new FakeDocumentWatcher(), _dispatcher, _roundTrip);
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
            new ColorCodeSyntaxHighlighter(),
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
            _stateStore,
            new FakePageSetupStore(),
            new StubCustomMarginsPrompt(answer: null),
            new FakePrintPreview());
    }

    [Fact]
    public async Task PersistState_RecordsEveryPanelsPlacement_INV067()
    {
        var workspace = CreateWorkspace();
        workspace.WorkspaceWidth = 2000d;

        // Source Panel Docked, Preview Panel Auto-Hidden, Editor Pane left Docked.
        workspace.ToggleSourcePanelCommand.Execute(null);
        workspace.TogglePreviewPanelCommand.Execute(null);
        workspace.TogglePinCommand.Execute(DockablePanel.PreviewPanel);

        await workspace.PersistStateAsync();

        var layout = _stateStore.SavedState!.Panels.ShouldNotBeNull();
        layout.EditorPane.ShouldBe(new PersistedPanelState(IsOpen: true, IsPinned: true));
        layout.SourcePanel.ShouldBe(new PersistedPanelState(IsOpen: true, IsPinned: true));
        layout.PreviewPanel.ShouldBe(new PersistedPanelState(IsOpen: true, IsPinned: false));
        layout.NavigationPanel.ShouldBe(new PersistedPanelState(IsOpen: false, IsPinned: true));
    }

    [Fact]
    public async Task Restore_ReopensADockedPanel_Docked_INV067()
    {
        _stateStore.StateToLoad = new WorkspaceState([], [], null, PanelLayout.Default with
        {
            SourcePanel = new PersistedPanelState(IsOpen: true, IsPinned: true),
        });
        var workspace = CreateWorkspace();
        workspace.WorkspaceWidth = 2000d;

        await workspace.RestoreAsync();

        workspace.IsSourcePanelOpen.ShouldBeTrue();
        workspace.IsSourcePanelVisible.ShouldBeTrue();
        workspace.RightAutoHideTabs.ShouldBeEmpty();
    }

    [Fact]
    public async Task Restore_ReopensAnAutoHiddenPanel_AutoHidden_NotDocked_INV067()
    {
        // Restoring is not a reopen, so it must not reset the pin the way opening a Closed panel
        // does (INV-062) — an Auto-Hidden panel comes back on its Auto-Hide Bar.
        _stateStore.StateToLoad = new WorkspaceState([], [], null, PanelLayout.Default with
        {
            PreviewPanel = new PersistedPanelState(IsOpen: true, IsPinned: false),
        });
        var workspace = CreateWorkspace();
        workspace.WorkspaceWidth = 2000d;

        await workspace.RestoreAsync();

        workspace.IsPreviewPanelOpen.ShouldBeTrue();
        workspace.IsPreviewPanelVisible.ShouldBeFalse();
        workspace.RightAutoHideTabs.Select(tab => tab.Panel).ShouldBe([DockablePanel.PreviewPanel]);
    }

    [Fact]
    public async Task Restore_LeavesAClosedPanelClosed_INV067()
    {
        _stateStore.StateToLoad = new WorkspaceState([], [], null, PanelLayout.Default);
        var workspace = CreateWorkspace();
        workspace.WorkspaceWidth = 2000d;

        await workspace.RestoreAsync();

        workspace.IsSourcePanelOpen.ShouldBeFalse();
        workspace.IsPreviewPanelOpen.ShouldBeFalse();
        workspace.SideDock.IsNavigationPanelOpen.ShouldBeFalse();
        workspace.IsEditorPaneVisible.ShouldBeTrue();
    }

    [Fact]
    public async Task Restore_WithNoPersistedLayout_KeepsTheDefaultLayout_INV067()
    {
        // A first run, or a state file written before the Panel Layout existed.
        _stateStore.StateToLoad = WorkspaceState.Empty;
        var workspace = CreateWorkspace();
        workspace.WorkspaceWidth = 2000d;

        await workspace.RestoreAsync();

        workspace.IsEditorPaneVisible.ShouldBeTrue();
        workspace.IsSourcePanelOpen.ShouldBeFalse();
        workspace.IsPreviewPanelOpen.ShouldBeFalse();
        workspace.LeftAutoHideTabs.ShouldBeEmpty();
        workspace.RightAutoHideTabs.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, true)]    // both Document Panes Closed
    [InlineData(true, false)]    // Editor Pane Auto-Hidden with no Source Panel to stand in for it
    public async Task Restore_WithALayoutThatStrandsTheDocumentPanes_DocksTheEditorPane_INV067(
        bool editorOpen,
        bool editorPinned)
    {
        _stateStore.StateToLoad = new WorkspaceState([], [], null, PanelLayout.Default with
        {
            EditorPane = new PersistedPanelState(editorOpen, editorPinned),
        });
        var workspace = CreateWorkspace();
        workspace.WorkspaceWidth = 2000d;

        await workspace.RestoreAsync();

        workspace.IsEditorPaneVisible.ShouldBeTrue();
    }

    [Fact]
    public async Task Restore_WithoutTheFolderItBrowses_LeavesTheFolderPanelClosed_INV067()
    {
        // The persisted root has gone, so no Folder Workspace restores (INV-045) — its panel must
        // not come back on an empty tree.
        _stateStore.StateToLoad = new WorkspaceState([], [], @"C:\gone", PanelLayout.Default with
        {
            FolderPanel = new PersistedPanelState(IsOpen: true, IsPinned: true),
        });
        _folderReader.MissingRoots.Add(@"C:\gone");
        var workspace = CreateWorkspace();
        workspace.WorkspaceWidth = 2000d;

        await workspace.RestoreAsync();

        workspace.Folder.IsFolderPanelVisible.ShouldBeFalse();
    }

    [Fact]
    public async Task Restore_WithTheFolderItBrowses_HonoursAClosedFolderPanel_INV067()
    {
        // The folder restores, but its panel was closed when the app was last shut down.
        _stateStore.StateToLoad = new WorkspaceState([], [], @"C:\notes", PanelLayout.Default);
        var workspace = CreateWorkspace();
        workspace.WorkspaceWidth = 2000d;

        await workspace.RestoreAsync();

        workspace.Folder.HasFolder.ShouldBeTrue();
        workspace.Folder.IsFolderPanelVisible.ShouldBeFalse();
    }

    [Fact]
    public void TogglingAPanel_PersistsTheLayout_INV067()
    {
        var workspace = CreateWorkspace();
        workspace.WorkspaceWidth = 2000d;
        var before = _stateStore.SaveCount;

        workspace.ToggleSourcePanelCommand.Execute(null);

        _stateStore.SaveCount.ShouldBeGreaterThan(before);
        _stateStore.SavedState!.Panels!.SourcePanel.IsOpen.ShouldBeTrue();
    }

    [Fact]
    public void ResizingTheWorkspace_DoesNotPersist_INV067()
    {
        // Compact Layout collapses panels for width (INV-059), but that is a resolution, not a
        // change of Placement — a drag of the window edge must not write the state file per pixel.
        var workspace = CreateWorkspace();
        workspace.WorkspaceWidth = 2000d;
        workspace.ToggleSourcePanelCommand.Execute(null);
        var after = _stateStore.SaveCount;

        workspace.WorkspaceWidth = 700d;
        workspace.WorkspaceWidth = 400d;
        workspace.WorkspaceWidth = 2000d;

        _stateStore.SaveCount.ShouldBe(after);
    }

    [Fact]
    public void Constructor_DoesNotPersist_SoItNeverClobbersTheSavedLayout_INV067()
    {
        // The Workspace is constructed before Restore runs; persisting the default layout there
        // would overwrite the very state Restore is about to read.
        CreateWorkspace();

        _stateStore.SaveCount.ShouldBe(0);
    }
}
