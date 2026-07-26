using System.Linq;
using Application;
using Infrastructure.Markdown;
using Shouldly;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for the Workspace's Tabs: Pin Tab and Unpin Tab moving a Tab between the Pinned Row and the
/// ordinary row without disturbing anything else (INV-071), and the two bulk closes — Close All Tabs
/// and Close All But Pinned — obeying INV-010 for every Tab they touch (INV-072).
/// </summary>
public sealed class WorkspaceViewModelTabsTests
{
    private const string FirstPath = @"C:\docs\first.md";
    private const string SecondPath = @"C:\docs\second.md";
    private const string ThirdPath = @"C:\docs\third.md";

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
            new StubDiagramBuilder(result: null),
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

    /// <summary>
    /// A Workspace holding one Tab per given path and nothing else — the empty Tab the constructor
    /// seeds is closed, so the rows hold exactly the opened documents in the order given.
    /// </summary>
    private async Task<WorkspaceViewModel> WorkspaceWithTabsAsync(params string[] paths)
    {
        foreach (var path in paths)
        {
            _store.Seed(path, $"# {System.IO.Path.GetFileNameWithoutExtension(path)}");
        }

        var workspace = CreateWorkspace();
        var seeded = workspace.ActiveSession!;

        foreach (var path in paths)
        {
            await workspace.OpenPathAsync(path);
        }

        await workspace.CloseSessionAsync(seeded);
        return workspace;
    }

    private static EditorSessionViewModel TabFor(WorkspaceViewModel workspace, string path) =>
        workspace.Sessions.Single(session =>
            string.Equals(session.FilePath, path, StringComparison.OrdinalIgnoreCase));

    // ---------------------------------------------------------------------------------------------
    // INV-071 — Pinning a Tab moves it to the Pinned Row and changes nothing else.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task PinnedRow_IsEmptyUntilATabIsPinned_INV071()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath);

        // The Pinned Row takes no space at all while nothing is pinned.
        workspace.PinnedTabs.ShouldBeEmpty();
        workspace.HasPinnedTabs.ShouldBeFalse();
        workspace.UnpinnedTabs.Count.ShouldBe(2);
    }

    [Fact]
    public async Task OrdinaryRow_CollapsesWhenItsLastTabIsPinnedAway_INV071()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath);

        workspace.IsOrdinaryRowVisible.ShouldBeTrue();

        await workspace.PinTabAsync(TabFor(workspace, FirstPath));

        // Every Tab is now pinned, so the Ordinary Row has none: it collapses rather than leaving an
        // empty band under the Pinned Row.
        workspace.UnpinnedTabs.ShouldBeEmpty();
        workspace.IsOrdinaryRowVisible.ShouldBeFalse();
        workspace.HasPinnedTabs.ShouldBeTrue();
    }

    [Fact]
    public async Task OrdinaryRow_CollapsesAfterCloseAllButPinned_INV071()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath, ThirdPath);
        await workspace.PinTabAsync(TabFor(workspace, FirstPath));

        await workspace.CloseAllButPinnedAsync();

        workspace.IsOrdinaryRowVisible.ShouldBeFalse();
    }

    [Fact]
    public async Task OrdinaryRow_ReturnsAsSoonAsATabJoinsIt_INV071()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath);
        await workspace.PinTabAsync(TabFor(workspace, FirstPath));
        workspace.IsOrdinaryRowVisible.ShouldBeFalse();

        workspace.New();

        // A new Tab joins the Ordinary Row, which comes back the moment it has something to show.
        workspace.IsOrdinaryRowVisible.ShouldBeTrue();
    }

    [Fact]
    public async Task OrdinaryRow_ReturnsWhenAPinnedTabIsUnpinned_INV071()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath);
        var first = TabFor(workspace, FirstPath);
        await workspace.PinTabAsync(first);

        await workspace.UnpinTabAsync(first);

        workspace.IsOrdinaryRowVisible.ShouldBeTrue();
        workspace.HasPinnedTabs.ShouldBeFalse();
    }

    [Fact]
    public async Task OrdinaryRow_IsShownEvenEmpty_WhenNothingIsPinned_INV071()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath);

        await workspace.CloseAllTabsAsync();

        // The Workspace is empty and nothing is pinned. The Ordinary Row stays shown so the strip —
        // and the new-tab button beside it — does not vanish along with the last Tab (INV-008).
        workspace.Sessions.ShouldBeEmpty();
        workspace.HasPinnedTabs.ShouldBeFalse();
        workspace.IsOrdinaryRowVisible.ShouldBeTrue();
    }

    [Fact]
    public async Task Pin_MovesTheTabToTheEndOfThePinnedRow_INV071()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath, ThirdPath);
        var first = TabFor(workspace, FirstPath);
        var third = TabFor(workspace, ThirdPath);

        await workspace.PinTabAsync(third);
        await workspace.PinTabAsync(first);

        // A Tab joins the end of the row it moves to, so the Pinned Row is in pin order — not in the
        // order the Tabs happened to be opened.
        workspace.PinnedTabs.ShouldBe([third, first]);
        workspace.HasPinnedTabs.ShouldBeTrue();
        third.IsPinned.ShouldBeTrue();
        first.IsPinned.ShouldBeTrue();

        // ...and it has left the ordinary row, which keeps the order of what remains.
        workspace.UnpinnedTabs.ShouldBe([TabFor(workspace, SecondPath)]);
    }

    [Fact]
    public async Task Unpin_MovesTheTabToTheEndOfTheOrdinaryRow_INV071()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath, ThirdPath);
        var first = TabFor(workspace, FirstPath);

        await workspace.PinTabAsync(first);
        await workspace.UnpinTabAsync(first);

        // A row is ordered by when its Tabs joined it, so the unpinned Tab lands at the end rather
        // than back where it started.
        first.IsPinned.ShouldBeFalse();
        workspace.PinnedTabs.ShouldBeEmpty();
        workspace.UnpinnedTabs.ShouldBe([
            TabFor(workspace, SecondPath),
            TabFor(workspace, ThirdPath),
            first,
        ]);
    }

    [Fact]
    public async Task TheTwoRows_PartitionTheOpenTabs_INV071()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath, ThirdPath);
        await workspace.PinTabAsync(TabFor(workspace, SecondPath));

        // Every open Tab is in exactly one row, and the two rows together are the Tabs in Tab order —
        // the Pinned Row first.
        workspace.PinnedTabs.Intersect(workspace.UnpinnedTabs).ShouldBeEmpty();
        workspace.PinnedTabs.Concat(workspace.UnpinnedTabs).ShouldBe(workspace.Sessions);
        workspace.Sessions.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Pin_LeavesTheActiveSessionAndTheDocumentAlone_INV071()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath);
        var first = TabFor(workspace, FirstPath);
        var second = TabFor(workspace, SecondPath);
        workspace.ActiveSession = second;
        var sourceBefore = first.Markdown;

        await workspace.PinTabAsync(first);

        // Pinning the Tab the user is not on does not steal them away from the one they are on, and
        // it never touches the Markdown Document.
        workspace.ActiveSession.ShouldBe(second);
        first.Markdown.ShouldBe(sourceBefore);
        first.HasUnsavedEdits.ShouldBeFalse();
    }

    [Fact]
    public async Task Pin_TheActiveSessionsOwnTab_LeavesItActive_INV071()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath);
        var second = TabFor(workspace, SecondPath);
        workspace.ActiveSession = second;

        await workspace.PinTabAsync(second);

        // Moving a Tab between rows never ends its Editor Session or deactivates it.
        workspace.ActiveSession.ShouldBe(second);
        workspace.PinnedTabs.ShouldBe([second]);
    }

    [Fact]
    public async Task Pin_IsIgnoredForATabAlreadyPinned_INV071()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath);
        var first = TabFor(workspace, FirstPath);

        await workspace.PinTabAsync(first);
        await workspace.PinTabAsync(first);

        // No Tab is ever lost or duplicated by the move.
        workspace.PinnedTabs.ShouldBe([first]);
        workspace.Sessions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Persist_RecordsWhichTabsArePinned_INV071()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath);

        await workspace.PinTabAsync(TabFor(workspace, SecondPath));

        // Which Tabs are pinned is part of the Workspace State (INV-037).
        _stateStore.SavedState.ShouldNotBeNull();
        _stateStore.SavedState!.PinnedDocuments.ShouldBe([SecondPath]);

        // ...and Tab order puts the Pinned Row first.
        _stateStore.SavedState.OpenDocuments.ShouldBe([SecondPath, FirstPath]);
    }

    [Fact]
    public async Task Persist_NeverRecordsAnUnsavedTabsPin_INV071()
    {
        _store.Seed(FirstPath, "# First");
        var workspace = CreateWorkspace();
        var untitled = workspace.ActiveSession!;
        await workspace.OpenPathAsync(FirstPath);

        await workspace.PinTabAsync(untitled);

        // An unsaved Tab can be pinned, but there is no path to record the pin against (INV-037).
        untitled.IsPinned.ShouldBeTrue();
        _stateStore.SavedState!.PinnedDocuments.ShouldBeEmpty();
        _stateStore.SavedState.OpenDocuments.ShouldBe([FirstPath]);
    }

    [Fact]
    public async Task Restore_ReopensAPinnedTabPinned_INV071()
    {
        _store.Seed(FirstPath, "# First");
        _store.Seed(SecondPath, "# Second");
        _stateStore.StateToLoad = new WorkspaceState([SecondPath, FirstPath], [])
        {
            PinnedDocuments = [SecondPath],
        };
        var workspace = CreateWorkspace();

        await workspace.RestoreAsync();

        workspace.PinnedTabs.Select(tab => tab.FilePath).ShouldBe([SecondPath]);
        workspace.UnpinnedTabs.Select(tab => tab.FilePath).ShouldBe([FirstPath]);
    }

    [Fact]
    public async Task Restore_FromAStateWithNoPins_PinsNothing_INV071()
    {
        _store.Seed(FirstPath, "# First");
        _stateStore.StateToLoad = new WorkspaceState([FirstPath], []);
        var workspace = CreateWorkspace();

        await workspace.RestoreAsync();

        // A state file written before pins existed restores nothing pinned rather than failing.
        workspace.PinnedTabs.ShouldBeEmpty();
        workspace.UnpinnedTabs.Count.ShouldBe(1);
    }

    // ---------------------------------------------------------------------------------------------
    // INV-072 — Closing many Tabs never silently discards unsaved edits.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task CloseAll_ClosesEveryTabIncludingPinned_INV072()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath, ThirdPath);
        await workspace.PinTabAsync(TabFor(workspace, FirstPath));

        await workspace.CloseAllTabsAsync();

        // A Pinned Tab is not "pinned open" — Close All Tabs closes it like any other, leaving the
        // Workspace empty rather than re-seeding a fresh Tab (INV-008).
        workspace.Sessions.ShouldBeEmpty();
        workspace.PinnedTabs.ShouldBeEmpty();
        workspace.UnpinnedTabs.ShouldBeEmpty();
        workspace.ActiveSession.ShouldBeNull();
        workspace.IsWorkspaceEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task CloseAllButPinned_LeavesThePinnedRowIntact_INV072()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath, ThirdPath);
        var first = TabFor(workspace, FirstPath);
        var third = TabFor(workspace, ThirdPath);
        await workspace.PinTabAsync(first);
        await workspace.PinTabAsync(third);

        await workspace.CloseAllButPinnedAsync();

        // The bulk close a pin protects a Tab from: the ordinary row empties, the Pinned Row does not.
        workspace.PinnedTabs.ShouldBe([first, third]);
        workspace.UnpinnedTabs.ShouldBeEmpty();
        workspace.Sessions.Count.ShouldBe(2);
        workspace.ActiveSession.ShouldNotBeNull();
    }

    [Fact]
    public async Task CloseAllButPinned_WithNothingPinned_EmptiesTheWorkspace_INV072()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath);

        await workspace.CloseAllButPinnedAsync();

        workspace.Sessions.ShouldBeEmpty();
        workspace.ActiveSession.ShouldBeNull();
    }

    [Fact]
    public async Task CloseAll_PromptsForEachTabWithUnsavedEdits_INV072()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath, ThirdPath);
        TabFor(workspace, FirstPath).Markdown = "# Edited first";
        TabFor(workspace, ThirdPath).Markdown = "# Edited third";
        _prompt.Decision = UnsavedEditsDecision.Discard;

        await workspace.CloseAllTabsAsync();

        // Every Tab holding unsaved edits is asked about in its turn — a bulk close does not weaken
        // INV-010; the clean Tab is closed without a question.
        _prompt.ConfirmCount.ShouldBe(2);
        _prompt.DocumentNames.ShouldBe(["first.md", "third.md"]);
        workspace.Sessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task CloseAll_WhenCancelledOnOneTab_StopsAndKeepsTheRest_INV072()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath, ThirdPath);
        foreach (var tab in workspace.Sessions.ToList())
        {
            tab.Markdown = "# Edited";
        }

        // Cancel on the second Tab; the first is discarded before it and the third is never reached.
        _prompt.DecisionFor = name => name == "second.md"
            ? UnsavedEditsDecision.Cancel
            : UnsavedEditsDecision.Discard;

        await workspace.CloseAllTabsAsync();

        // Cancel on any one Tab stops the whole action: that Tab and every Tab not yet reached stay
        // open, while the Tab already closed by an explicit Discard stays closed.
        workspace.Sessions.Select(tab => tab.FilePath).ShouldBe([SecondPath, ThirdPath]);
        _prompt.ConfirmCount.ShouldBe(2);
        workspace.ActiveSession.ShouldNotBeNull();
    }

    [Fact]
    public async Task CloseAllButPinned_WhenCancelled_KeepsThatTabAndThePinnedRow_INV072()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath, ThirdPath);
        await workspace.PinTabAsync(TabFor(workspace, FirstPath));
        TabFor(workspace, SecondPath).Markdown = "# Edited second";
        _prompt.Decision = UnsavedEditsDecision.Cancel;

        await workspace.CloseAllButPinnedAsync();

        // The Pinned Tab was never a candidate, and Cancel keeps the Tab it was asked about along
        // with the one after it.
        workspace.PinnedTabs.Select(tab => tab.FilePath).ShouldBe([FirstPath]);
        workspace.UnpinnedTabs.Select(tab => tab.FilePath).ShouldBe([SecondPath, ThirdPath]);
    }

    [Fact]
    public async Task CloseAll_SavesATabWhoseUnsavedEditsTheUserChoosesToKeep_INV072()
    {
        var workspace = await WorkspaceWithTabsAsync(FirstPath, SecondPath);
        TabFor(workspace, FirstPath).Markdown = "# Saved on the way out";
        _prompt.Decision = UnsavedEditsDecision.Save;

        await workspace.CloseAllTabsAsync();

        // Save persists before closing, exactly as a single Close Tab does (INV-010).
        _store.SavedText(FirstPath).ShouldBe("# Saved on the way out");
        workspace.Sessions.ShouldBeEmpty();
    }
}
