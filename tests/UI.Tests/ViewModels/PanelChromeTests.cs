using Shouldly;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="PanelChrome"/> — the pure rules behind every Dockable Panel's Panel
/// Placement: how Docked / Auto-Hidden / Closed derive from a panel's open and pinned state, the
/// guards that keep a Document Pane Docked at every moment (INV-063), and the Auto-Hide Bar
/// projections that list exactly the Auto-Hidden panels (INV-062).
/// </summary>
public sealed class PanelChromeTests
{
    private static readonly PanelState Docked = new(IsOpen: true, IsPinned: true);
    private static readonly PanelState AutoHidden = new(IsOpen: true, IsPinned: false);
    private static readonly PanelState Closed = new(IsOpen: false, IsPinned: true);

    private static PanelChromeState State(
        PanelState? editor = null,
        PanelState? source = null,
        PanelState? preview = null,
        PanelState? folder = null,
        PanelState? navigation = null) =>
        new(editor ?? Docked, source ?? Closed, preview ?? Closed, folder ?? Closed, navigation ?? Closed);

    [Fact]
    public void Default_HasTheEditorPaneDocked_AndEveryOtherPanelClosed_INV062()
    {
        var state = PanelChromeState.Default;

        PanelChrome.PlacementOf(state, DockablePanel.EditorPane).ShouldBe(PanelPlacement.Docked);
        PanelChrome.PlacementOf(state, DockablePanel.SourcePanel).ShouldBe(PanelPlacement.Closed);
        PanelChrome.PlacementOf(state, DockablePanel.PreviewPanel).ShouldBe(PanelPlacement.Closed);
        PanelChrome.PlacementOf(state, DockablePanel.FolderPanel).ShouldBe(PanelPlacement.Closed);
        PanelChrome.PlacementOf(state, DockablePanel.NavigationPanel).ShouldBe(PanelPlacement.Closed);
    }

    [Theory]
    [InlineData(true, true, PanelPlacement.Docked)]
    [InlineData(true, false, PanelPlacement.AutoHidden)]
    [InlineData(false, true, PanelPlacement.Closed)]
    [InlineData(false, false, PanelPlacement.Closed)]
    public void PlacementOf_DerivesFromOpenAndPinned_INV062(bool isOpen, bool isPinned, PanelPlacement expected)
    {
        var state = State(preview: new PanelState(isOpen, isPinned));

        PanelChrome.PlacementOf(state, DockablePanel.PreviewPanel).ShouldBe(expected);
    }

    [Fact]
    public void CanClose_AClosedPanel_IsFalse_INV062()
    {
        var state = State(preview: Closed);

        PanelChrome.CanClose(state, DockablePanel.PreviewPanel).ShouldBeFalse();
    }

    [Fact]
    public void CanClose_AnOpenPanelThatIsNotADocumentPane_IsAlwaysTrue_INV062()
    {
        var state = State(preview: Docked, folder: AutoHidden, navigation: Docked);

        PanelChrome.CanClose(state, DockablePanel.PreviewPanel).ShouldBeTrue();
        PanelChrome.CanClose(state, DockablePanel.FolderPanel).ShouldBeTrue();
        PanelChrome.CanClose(state, DockablePanel.NavigationPanel).ShouldBeTrue();
    }

    [Fact]
    public void CanClose_TheDockedEditorPane_WhileTheSourcePanelIsNotDocked_IsFalse_INV063()
    {
        PanelChrome.CanClose(State(editor: Docked, source: Closed), DockablePanel.EditorPane).ShouldBeFalse();
        PanelChrome.CanClose(State(editor: Docked, source: AutoHidden), DockablePanel.EditorPane).ShouldBeFalse();
    }

    [Fact]
    public void CanClose_TheDockedEditorPane_WhileTheSourcePanelIsDocked_IsTrue_INV063()
    {
        var state = State(editor: Docked, source: Docked);

        PanelChrome.CanClose(state, DockablePanel.EditorPane).ShouldBeTrue();
    }

    [Fact]
    public void CanClose_TheDockedSourcePanel_WhileTheEditorPaneIsNotDocked_IsFalse_INV063()
    {
        PanelChrome.CanClose(State(editor: Closed, source: Docked), DockablePanel.SourcePanel).ShouldBeFalse();
        PanelChrome.CanClose(State(editor: AutoHidden, source: Docked), DockablePanel.SourcePanel).ShouldBeFalse();
    }

    [Fact]
    public void CanClose_AnAutoHiddenDocumentPane_WhileTheOtherIsDocked_IsTrue_INV063()
    {
        var state = State(editor: AutoHidden, source: Docked);

        // Closing the Auto-Hidden Editor Pane leaves the Docked Source Panel — the rule holds.
        PanelChrome.CanClose(state, DockablePanel.EditorPane).ShouldBeTrue();
    }

    [Fact]
    public void CanUnpin_ADockedPanel_IsTrue_INV062()
    {
        var state = State(preview: Docked);

        PanelChrome.CanUnpin(state, DockablePanel.PreviewPanel).ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, false)]  // Auto-Hidden: already unpinned.
    [InlineData(false, true)]  // Closed: nothing to unpin.
    public void CanUnpin_APanelThatIsNotDocked_IsFalse_INV062(bool isOpen, bool isPinned)
    {
        var state = State(preview: new PanelState(isOpen, isPinned));

        PanelChrome.CanUnpin(state, DockablePanel.PreviewPanel).ShouldBeFalse();
    }

    [Fact]
    public void CanUnpin_TheDockedEditorPane_WhileTheSourcePanelIsNotDocked_IsFalse_INV063()
    {
        PanelChrome.CanUnpin(State(editor: Docked, source: Closed), DockablePanel.EditorPane).ShouldBeFalse();
        PanelChrome.CanUnpin(State(editor: Docked, source: AutoHidden), DockablePanel.EditorPane).ShouldBeFalse();
    }

    [Fact]
    public void CanUnpin_TheDockedEditorPane_WhileTheSourcePanelIsDocked_IsTrue_INV063()
    {
        var state = State(editor: Docked, source: Docked);

        PanelChrome.CanUnpin(state, DockablePanel.EditorPane).ShouldBeTrue();
    }

    [Fact]
    public void CanUnpin_TheDockedSourcePanel_WhileTheEditorPaneIsNotDocked_IsFalse_INV063()
    {
        var state = State(editor: Closed, source: Docked);

        PanelChrome.CanUnpin(state, DockablePanel.SourcePanel).ShouldBeFalse();
    }

    [Fact]
    public void CanPin_AnAutoHiddenPanel_IsTrue_INV062()
    {
        var state = State(preview: AutoHidden);

        PanelChrome.CanPin(state, DockablePanel.PreviewPanel).ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, true)]   // Docked: already pinned.
    [InlineData(false, true)]  // Closed: nothing to pin.
    public void CanPin_APanelThatIsNotAutoHidden_IsFalse_INV062(bool isOpen, bool isPinned)
    {
        var state = State(preview: new PanelState(isOpen, isPinned));

        PanelChrome.CanPin(state, DockablePanel.PreviewPanel).ShouldBeFalse();
    }

    [Fact]
    public void LeftAutoHideTabs_ListExactlyTheAutoHiddenLeftPanels_InOrder_INV062()
    {
        var state = State(editor: AutoHidden, source: Docked, folder: AutoHidden, navigation: AutoHidden);

        var tabs = PanelChrome.LeftAutoHideTabs(state);

        tabs.Select(tab => tab.Panel).ShouldBe(
            [DockablePanel.EditorPane, DockablePanel.FolderPanel, DockablePanel.NavigationPanel]);
    }

    [Fact]
    public void RightAutoHideTabs_ListExactlyTheAutoHiddenRightPanels_InOrder_INV062()
    {
        var state = State(source: AutoHidden, preview: AutoHidden);

        var tabs = PanelChrome.RightAutoHideTabs(state);

        tabs.Select(tab => tab.Panel).ShouldBe([DockablePanel.SourcePanel, DockablePanel.PreviewPanel]);
    }

    [Fact]
    public void AutoHideBars_WithNoAutoHiddenPanel_AreEmpty_INV062()
    {
        var state = State(source: Docked, preview: Docked, folder: Docked, navigation: Docked);

        PanelChrome.LeftAutoHideTabs(state).ShouldBeEmpty();
        PanelChrome.RightAutoHideTabs(state).ShouldBeEmpty();
    }

    [Fact]
    public void AutoHideBars_ExcludeClosedPanels_INV062()
    {
        var state = State(source: Closed, preview: Closed, folder: Closed, navigation: Closed);

        PanelChrome.LeftAutoHideTabs(state).ShouldBeEmpty();
        PanelChrome.RightAutoHideTabs(state).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(DockablePanel.EditorPane, "Editor")]
    [InlineData(DockablePanel.SourcePanel, "Source")]
    [InlineData(DockablePanel.PreviewPanel, "Diagram Preview")]
    [InlineData(DockablePanel.FolderPanel, "Folder")]
    [InlineData(DockablePanel.NavigationPanel, "Outline")]
    public void AutoHideTab_For_NamesThePanelAsItsPanelHeaderDoes_INV062(DockablePanel panel, string title)
    {
        AutoHideTab.For(panel).Title.ShouldBe(title);
        AutoHideTab.For(panel).Panel.ShouldBe(panel);
    }
}
