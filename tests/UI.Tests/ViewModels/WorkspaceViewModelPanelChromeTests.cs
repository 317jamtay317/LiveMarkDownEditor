using Application;
using Shouldly;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for the <see cref="WorkspaceViewModel"/>'s Panel Chrome surface — the close, pin, and
/// flyout behaviours over every Dockable Panel (INV-062) and the Document Pane rule that always
/// keeps the Editor Pane or the Source Panel Docked (INV-063).
/// </summary>
public sealed class WorkspaceViewModelPanelChromeTests
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
    public void Constructor_StartsWithTheEditorPaneDocked_AndEmptyAutoHideBars_INV062()
    {
        var workspace = CreateWorkspace();

        workspace.IsEditorPaneVisible.ShouldBeTrue();
        workspace.IsEditorPaneOpen.ShouldBeTrue();
        workspace.IsEditorPanePinned.ShouldBeTrue();
        workspace.LeftAutoHideTabs.ShouldBeEmpty();
        workspace.RightAutoHideTabs.ShouldBeEmpty();
        workspace.HasOpenFlyout.ShouldBeFalse();
    }

    [Fact]
    public void ToggleEditorPane_WhileTheSourcePanelIsNotDocked_IsUnavailable_INV063()
    {
        var workspace = CreateWorkspace();

        // The Editor Pane is the only Docked Document Pane: closing it is greyed out, not refused late.
        workspace.ToggleEditorPaneCommand.CanExecute(null).ShouldBeFalse();
        workspace.ClosePanelCommand.CanExecute(DockablePanel.EditorPane).ShouldBeFalse();
        workspace.TogglePinCommand.CanExecute(DockablePanel.EditorPane).ShouldBeFalse();

        workspace.ToggleEditorPaneCommand.Execute(null);
        workspace.IsEditorPaneVisible.ShouldBeTrue();
    }

    [Fact]
    public void ToggleEditorPane_WhileTheSourcePanelIsDocked_ClosesAndReopensTheEditorPane_INV063()
    {
        var workspace = CreateWorkspace();
        workspace.ActiveSession!.Markdown = "# Title";
        workspace.ToggleSourcePanelCommand.Execute(null);

        workspace.ToggleEditorPaneCommand.Execute(null);

        // The Source Panel becomes the primary Document Pane and fills the editor's place.
        workspace.IsEditorPaneVisible.ShouldBeFalse();
        workspace.IsEditorPaneOpen.ShouldBeFalse();
        workspace.IsSourcePanelPrimary.ShouldBeTrue();
        workspace.IsSourceSplitterVisible.ShouldBeFalse();

        workspace.ToggleEditorPaneCommand.Execute(null);
        workspace.IsEditorPaneVisible.ShouldBeTrue();
        workspace.IsSourcePanelPrimary.ShouldBeFalse();
        workspace.IsSourceSplitterVisible.ShouldBeTrue();

        // Closing and reopening the Editor Pane is presentation-only (INV-062).
        workspace.ActiveSession.Markdown.ShouldBe("# Title");
    }

    [Fact]
    public void ToggleSourcePanel_WhileItIsTheOnlyDockedDocumentPane_IsUnavailable_INV063()
    {
        var workspace = CreateWorkspace();
        workspace.ToggleSourcePanelCommand.Execute(null);
        workspace.ToggleEditorPaneCommand.Execute(null);

        // The Source Panel now stands alone: its toggle cannot close it until the editor is back.
        workspace.ToggleSourcePanelCommand.CanExecute(null).ShouldBeFalse();
        workspace.ClosePanelCommand.CanExecute(DockablePanel.SourcePanel).ShouldBeFalse();

        workspace.ToggleSourcePanelCommand.Execute(null);
        workspace.IsSourcePanelVisible.ShouldBeTrue();
    }

    [Fact]
    public void TogglePin_OnTheSourcePanel_MovesItToTheRightAutoHideBar_WithoutChangingTheDocument_INV062()
    {
        var workspace = CreateWorkspace();
        workspace.ActiveSession!.Markdown = "# Title";
        workspace.ToggleSourcePanelCommand.Execute(null);

        workspace.TogglePinCommand.Execute(DockablePanel.SourcePanel);

        // Auto-Hidden: open but unpinned — off the layout, on the right Auto-Hide Bar.
        workspace.IsSourcePanelOpen.ShouldBeTrue();
        workspace.IsSourcePanelPinned.ShouldBeFalse();
        workspace.IsSourcePanelVisible.ShouldBeFalse();
        workspace.RightAutoHideTabs.Select(tab => tab.Panel).ShouldBe([DockablePanel.SourcePanel]);
        workspace.ActiveSession.Markdown.ShouldBe("# Title");
    }

    [Fact]
    public void TogglePin_OnAnAutoHiddenPanel_DocksItBack_INV062()
    {
        var workspace = CreateWorkspace();
        workspace.ToggleSourcePanelCommand.Execute(null);
        workspace.TogglePinCommand.Execute(DockablePanel.SourcePanel);

        workspace.TogglePinCommand.Execute(DockablePanel.SourcePanel);

        workspace.IsSourcePanelVisible.ShouldBeTrue();
        workspace.IsSourcePanelPinned.ShouldBeTrue();
        workspace.RightAutoHideTabs.ShouldBeEmpty();
    }

    [Fact]
    public void TogglePin_OnTheEditorPane_WhileTheSourcePanelIsDocked_MovesItToTheLeftAutoHideBar_INV063()
    {
        var workspace = CreateWorkspace();
        workspace.ToggleSourcePanelCommand.Execute(null);

        workspace.TogglePinCommand.Execute(DockablePanel.EditorPane);

        workspace.IsEditorPaneVisible.ShouldBeFalse();
        workspace.IsEditorPaneOpen.ShouldBeTrue();
        workspace.LeftAutoHideTabs.Select(tab => tab.Panel).ShouldBe([DockablePanel.EditorPane]);
        workspace.IsSourcePanelPrimary.ShouldBeTrue();
    }

    [Fact]
    public void ToggleFlyout_OpensAndDismissesThePanelFlyout_WithoutChangingPlacement_INV062()
    {
        var workspace = CreateWorkspace();
        workspace.ActiveSession!.Markdown = "# Title";
        workspace.ToggleSourcePanelCommand.Execute(null);
        workspace.TogglePinCommand.Execute(DockablePanel.SourcePanel);

        workspace.ToggleFlyoutCommand.Execute(DockablePanel.SourcePanel);

        workspace.IsSourcePanelFlyoutOpen.ShouldBeTrue();
        workspace.HasOpenFlyout.ShouldBeTrue();
        // The flyout shows the panel without docking it: its Placement is unchanged.
        workspace.IsSourcePanelVisible.ShouldBeFalse();
        workspace.RightAutoHideTabs.Select(tab => tab.Panel).ShouldBe([DockablePanel.SourcePanel]);

        workspace.ToggleFlyoutCommand.Execute(DockablePanel.SourcePanel);
        workspace.IsSourcePanelFlyoutOpen.ShouldBeFalse();
        workspace.HasOpenFlyout.ShouldBeFalse();
        workspace.ActiveSession.Markdown.ShouldBe("# Title");
    }

    [Fact]
    public void Flyout_IsDismissed_WhenItsPanelIsPinnedBack_INV062()
    {
        var workspace = CreateWorkspace();
        workspace.ToggleSourcePanelCommand.Execute(null);
        workspace.TogglePinCommand.Execute(DockablePanel.SourcePanel);
        workspace.ToggleFlyoutCommand.Execute(DockablePanel.SourcePanel);

        // Pinned from the flyout's own Panel Header: the panel docks and the flyout goes with it.
        workspace.TogglePinCommand.Execute(DockablePanel.SourcePanel);

        workspace.IsSourcePanelFlyoutOpen.ShouldBeFalse();
        workspace.IsSourcePanelVisible.ShouldBeTrue();
    }

    [Fact]
    public void DismissFlyout_ClosesTheOpenFlyout_AndIsUnavailableWithoutOne_INV062()
    {
        var workspace = CreateWorkspace();
        workspace.DismissFlyoutCommand.CanExecute(null).ShouldBeFalse();

        workspace.ToggleSourcePanelCommand.Execute(null);
        workspace.TogglePinCommand.Execute(DockablePanel.SourcePanel);
        workspace.ToggleFlyoutCommand.Execute(DockablePanel.SourcePanel);
        workspace.DismissFlyoutCommand.CanExecute(null).ShouldBeTrue();

        workspace.DismissFlyoutCommand.Execute(null);

        workspace.HasOpenFlyout.ShouldBeFalse();
        // Dismissing a flyout is not a layout change: the panel is still Auto-Hidden.
        workspace.RightAutoHideTabs.Select(tab => tab.Panel).ShouldBe([DockablePanel.SourcePanel]);
    }

    [Fact]
    public void ClosePanel_OnAnAutoHiddenPanel_RemovesItsTab_AndItsToggleReopensItDocked_INV062()
    {
        var workspace = CreateWorkspace();
        workspace.TogglePreviewPanelCommand.Execute(null);
        workspace.TogglePinCommand.Execute(DockablePanel.PreviewPanel);
        workspace.RightAutoHideTabs.Select(tab => tab.Panel).ShouldBe([DockablePanel.PreviewPanel]);

        workspace.ClosePanelCommand.Execute(DockablePanel.PreviewPanel);

        workspace.IsPreviewPanelOpen.ShouldBeFalse();
        workspace.RightAutoHideTabs.ShouldBeEmpty();

        // Reopened from the Command Bar, the panel comes back Docked — never straight to Auto-Hidden.
        workspace.TogglePreviewPanelCommand.Execute(null);
        workspace.IsPreviewPanelVisible.ShouldBeTrue();
        workspace.IsPreviewPanelPinned.ShouldBeTrue();
        workspace.RightAutoHideTabs.ShouldBeEmpty();
    }

    [Fact]
    public void UnpinTheNavigationPanel_MovesItsTabFromTheDockStripToTheLeftBar_INV062()
    {
        var workspace = CreateWorkspace();
        workspace.SideDock.ToggleNavigationPanelCommand.Execute(null);

        workspace.TogglePinCommand.Execute(DockablePanel.NavigationPanel);

        workspace.SideDock.IsNavigationTabVisible.ShouldBeFalse();
        workspace.SideDock.IsNavigationPanelOpen.ShouldBeTrue();
        workspace.LeftAutoHideTabs.Select(tab => tab.Panel).ShouldBe([DockablePanel.NavigationPanel]);

        // Closing the panel through its Command Bar toggle takes it off the bar too...
        workspace.SideDock.ToggleNavigationPanelCommand.Execute(null);
        workspace.LeftAutoHideTabs.ShouldBeEmpty();

        // ...and reopening it docks it, unpinned no more (INV-062).
        workspace.SideDock.ToggleNavigationPanelCommand.Execute(null);
        workspace.SideDock.IsNavigationTabVisible.ShouldBeTrue();
    }

    [Fact]
    public void UnpinTheFolderPanel_MovesItToTheLeftBar_AndClosePanelClosesIt_INV062()
    {
        var workspace = CreateWorkspace();
        workspace.Folder.ToggleFolderPanelCommand.Execute(null);
        workspace.SideDock.IsFolderTabVisible.ShouldBeTrue();

        workspace.TogglePinCommand.Execute(DockablePanel.FolderPanel);
        workspace.SideDock.IsFolderTabVisible.ShouldBeFalse();
        workspace.LeftAutoHideTabs.Select(tab => tab.Panel).ShouldBe([DockablePanel.FolderPanel]);

        workspace.ClosePanelCommand.Execute(DockablePanel.FolderPanel);

        workspace.Folder.IsFolderPanelVisible.ShouldBeFalse();
        workspace.LeftAutoHideTabs.ShouldBeEmpty();
    }

    [Fact]
    public void FlyoutOfASideDockPanel_PresentsItInTheDock_INV062()
    {
        var workspace = CreateWorkspace();
        workspace.Folder.ToggleFolderPanelCommand.Execute(null);
        workspace.TogglePinCommand.Execute(DockablePanel.FolderPanel);

        workspace.ToggleFlyoutCommand.Execute(DockablePanel.FolderPanel);

        workspace.IsFolderPanelFlyoutOpen.ShouldBeTrue();
        workspace.IsSideDockFlyoutOpen.ShouldBeTrue();
        workspace.SideDockDisplayedTab.ShouldBe(SideDockTab.Folder);
        workspace.DisplayedSideDockPanel.ShouldBe(DockablePanel.FolderPanel);
        workspace.IsSideDockPanelPinned.ShouldBeFalse();
    }

    [Fact]
    public void ANarrowWorkspace_NeverCollapsesThePrimarySourcePanel_INV063()
    {
        var workspace = CreateWorkspace();
        workspace.ToggleSourcePanelCommand.Execute(null);
        workspace.TogglePreviewPanelCommand.Execute(null);
        workspace.ToggleEditorPaneCommand.Execute(null);

        // 240 primary Source + 420 Preview = 660 > 500: the Preview collapses, the Source never does.
        workspace.WorkspaceWidth = 500;

        workspace.IsSourcePanelVisible.ShouldBeTrue();
        workspace.IsPreviewPanelVisible.ShouldBeFalse();

        workspace.WorkspaceWidth = 1400;
        workspace.IsPreviewPanelVisible.ShouldBeTrue();
    }
}
