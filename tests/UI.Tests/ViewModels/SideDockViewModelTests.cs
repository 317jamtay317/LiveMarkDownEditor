using Shouldly;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="SideDockViewModel"/> — the Side Dock that hosts the Folder Panel and the
/// Navigation Panel as tabs. It shows exactly the panels toggled on, presents one Selected tab at a
/// time, and coordinates the two without changing any document (INV-046).
/// </summary>
public sealed class SideDockViewModelTests
{
    private readonly StubFolderPicker _folderPicker = new();
    private readonly FakeMarkdownFolderReader _folderReader = new();
    private readonly FakeFolderWatcher _folderWatcher = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly List<string> _opened = [];

    private FolderWorkspaceViewModel CreateFolder() =>
        new(_folderPicker, _folderReader, _folderWatcher, _dispatcher)
        {
            OpenFile = path =>
            {
                _opened.Add(path);
                return Task.CompletedTask;
            },
        };

    private void ShowFolderPanel(FolderWorkspaceViewModel folder) =>
        folder.ToggleFolderPanelCommand.Execute(null);

    [Fact]
    public void Constructor_StartsHiddenWithNeitherTabShown_INV046()
    {
        var sideDock = new SideDockViewModel(CreateFolder());

        sideDock.IsVisible.ShouldBeFalse();
        sideDock.IsFolderTabVisible.ShouldBeFalse();
        sideDock.IsNavigationTabVisible.ShouldBeFalse();
    }

    [Fact]
    public void ToggleNavigationPanel_ShowsAndSelectsTheOutlineTab_INV046()
    {
        var sideDock = new SideDockViewModel(CreateFolder());

        sideDock.ToggleNavigationPanelCommand.Execute(null);

        sideDock.IsNavigationTabVisible.ShouldBeTrue();
        sideDock.IsVisible.ShouldBeTrue();
        sideDock.SelectedTab.ShouldBe(SideDockTab.Navigation);
    }

    [Fact]
    public void ToggleNavigationPanel_Twice_HidesTheDock_INV046()
    {
        var sideDock = new SideDockViewModel(CreateFolder());

        sideDock.ToggleNavigationPanelCommand.Execute(null);
        sideDock.ToggleNavigationPanelCommand.Execute(null);

        sideDock.IsNavigationTabVisible.ShouldBeFalse();
        sideDock.IsVisible.ShouldBeFalse();
    }

    [Fact]
    public void OpeningTheFolderPanel_ShowsAndSelectsTheFolderTab_INV046()
    {
        var folder = CreateFolder();
        var sideDock = new SideDockViewModel(folder);

        ShowFolderPanel(folder);

        sideDock.IsFolderTabVisible.ShouldBeTrue();
        sideDock.IsVisible.ShouldBeTrue();
        sideDock.SelectedTab.ShouldBe(SideDockTab.Folder);
    }

    [Fact]
    public void WithBothTabsShown_TogglingTheSelectedNavigationOff_SelectsTheFolder_INV046()
    {
        var folder = CreateFolder();
        var sideDock = new SideDockViewModel(folder);
        ShowFolderPanel(folder);                             // Folder tab shown and selected
        sideDock.ToggleNavigationPanelCommand.Execute(null); // Outline tab shown and now selected
        sideDock.SelectedTab.ShouldBe(SideDockTab.Navigation);

        sideDock.ToggleNavigationPanelCommand.Execute(null); // hide the Selected tab

        // The Folder tab is still shown, so it takes the selection rather than the dock going blank.
        sideDock.SelectedTab.ShouldBe(SideDockTab.Folder);
        sideDock.IsVisible.ShouldBeTrue();
    }

    [Fact]
    public void WithBothTabsShown_TogglingTheSelectedFolderOff_SelectsTheOutline_INV046()
    {
        var folder = CreateFolder();
        var sideDock = new SideDockViewModel(folder);
        sideDock.ToggleNavigationPanelCommand.Execute(null); // Outline tab shown and selected
        ShowFolderPanel(folder);                             // Folder tab shown and now selected
        sideDock.SelectedTab.ShouldBe(SideDockTab.Folder);

        folder.ToggleFolderPanelCommand.Execute(null);       // hide the Selected Folder tab

        sideDock.SelectedTab.ShouldBe(SideDockTab.Navigation);
        sideDock.IsVisible.ShouldBeTrue();
    }

    [Fact]
    public void SelectTab_SwitchesTheSelectedTab_WithoutHidingEither_INV046()
    {
        var folder = CreateFolder();
        var sideDock = new SideDockViewModel(folder);
        ShowFolderPanel(folder);
        sideDock.ToggleNavigationPanelCommand.Execute(null);

        sideDock.SelectFolderTabCommand.Execute(null);
        sideDock.SelectedTab.ShouldBe(SideDockTab.Folder);

        sideDock.SelectNavigationTabCommand.Execute(null);
        sideDock.SelectedTab.ShouldBe(SideDockTab.Navigation);

        sideDock.IsFolderTabVisible.ShouldBeTrue();
        sideDock.IsNavigationTabVisible.ShouldBeTrue();
    }

    [Fact]
    public void CoordinatingTheTabs_OpensNoDocument_INV046()
    {
        var folder = CreateFolder();
        var sideDock = new SideDockViewModel(folder);

        // Show, select, and hide the tabs every which way.
        ShowFolderPanel(folder);
        sideDock.ToggleNavigationPanelCommand.Execute(null);
        sideDock.SelectFolderTabCommand.Execute(null);
        sideDock.SelectNavigationTabCommand.Execute(null);
        sideDock.ToggleNavigationPanelCommand.Execute(null);
        folder.ToggleFolderPanelCommand.Execute(null);

        // Docking and selecting are presentation-only: no File was ever opened (INV-046).
        _opened.ShouldBeEmpty();
    }

    [Fact]
    public void HasVisibleTab_IsTrue_WhileAnyTabIsShown_INV059()
    {
        var sideDock = new SideDockViewModel(CreateFolder());
        sideDock.HasVisibleTab.ShouldBeFalse();

        sideDock.ToggleNavigationPanelCommand.Execute(null);

        sideDock.HasVisibleTab.ShouldBeTrue();
    }

    [Fact]
    public void WidthCollapse_HidesTheDock_WithoutLosingItsTabOrSelection_INV059()
    {
        var sideDock = new SideDockViewModel(CreateFolder());
        sideDock.ToggleNavigationPanelCommand.Execute(null);
        sideDock.IsVisible.ShouldBeTrue();

        // The window has grown too narrow for the dock beside the editor: it collapses (INV-059)...
        sideDock.SetWidthCollapsed(true);
        sideDock.IsVisible.ShouldBeFalse();

        // ...but its tab and selection are intact, so widening restores it exactly as it was.
        sideDock.IsNavigationTabVisible.ShouldBeTrue();
        sideDock.SelectedTab.ShouldBe(SideDockTab.Navigation);

        sideDock.SetWidthCollapsed(false);
        sideDock.IsVisible.ShouldBeTrue();
    }
}
