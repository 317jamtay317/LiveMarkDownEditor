using System.Windows.Input;
using Domain;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;
using static UI.Tests.Controls.FolderPanelHarness;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for <see cref="FolderPanel"/>: double-clicking a File in the Folder Tree activates it at any
/// depth — a File nested under a Folder opens exactly as one at the root does — while a Folder is left
/// to its native Expand/Collapse (INV-043). Delete File is reached from a File, by the Delete key or the
/// context menu, and never from a Folder (INV-081). A rebuild keeps the user's place (INV-044). Rename
/// File has its own tests in <see cref="FolderPanelRenameTests"/>.
/// </summary>
public sealed class FolderPanelTests
{
    [Fact]
    public void DoubleClicking_AFileAtTheRoot_ActivatesThatFile_INV043()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out var activated, "top.md", "Nested/deep.md");
            var row = Row(panel, "top.md");

            panel.ActivateAt(HeaderTextOf(row));

            activated.Select(entry => entry.RelativePath).ShouldBe(["top.md"]);
        });
    }

    [Fact]
    public void DoubleClicking_AFileInsideAFolder_ActivatesThatFile_INV043()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out var activated, "top.md", "Nested/deep.md");
            var row = Row(panel, "Nested", "deep.md");

            panel.ActivateAt(HeaderTextOf(row));

            activated.Select(entry => entry.RelativePath).ShouldBe(["Nested/deep.md"]);
        });
    }

    [Fact]
    public void DoubleClicking_AFileNestedTwoFoldersDeep_ActivatesThatFile_INV043()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out var activated, "Outer/Inner/buried.md");
            var row = Row(panel, "Outer", "Inner", "buried.md");

            panel.ActivateAt(HeaderTextOf(row));

            activated.Select(entry => entry.RelativePath).ShouldBe(["Outer/Inner/buried.md"]);
        });
    }

    [Fact]
    public void DoubleClicking_AFolder_ActivatesNothing_INV043()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out var activated, "Nested/deep.md");
            var row = Row(panel, "Nested");

            panel.ActivateAt(HeaderTextOf(row));

            activated.ShouldBeEmpty();
        });
    }

    [Fact]
    public void Selecting_AFolder_ReportsItAsTheSelectedEntry_INV080()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "Nested/deep.md");
            var row = Row(panel, "Nested");

            row.IsSelected = true;

            panel.SelectedEntry?.RelativePath.ShouldBe("Nested");
        });
    }

    [Fact]
    public void Selecting_AFile_ReportsItAsTheSelectedEntry_INV080()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "Nested/deep.md");
            var row = Row(panel, "Nested", "deep.md");

            row.IsSelected = true;

            panel.SelectedEntry?.RelativePath.ShouldBe("Nested/deep.md");
        });
    }

    [Fact]
    public void Selecting_AnEntry_ActivatesNothing_INV043()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out var activated, "Nested/deep.md");
            var row = Row(panel, "Nested", "deep.md");

            row.IsSelected = true;

            // Selecting a row is browsing; only a double-click or Enter opens a File (INV-043).
            activated.ShouldBeEmpty();
        });
    }

    [Fact]
    public void SelectedEntry_BeforeAnythingIsSelected_IsNothing_INV080()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "Nested/deep.md");

            panel.SelectedEntry.ShouldBeNull();
        });
    }

    [Fact]
    public void PressingDelete_OnASelectedFile_DeletesThatFile_INV081()
    {
        StaThread.Run(() =>
        {
            var panel = BuildDeletablePanel(out var deleted, "top.md", "Nested/deep.md");
            Row(panel, "Nested", "deep.md").IsSelected = true;

            PressKey(panel, Key.Delete);

            deleted.Select(entry => entry.RelativePath).ShouldBe(["Nested/deep.md"]);
        });
    }

    [Fact]
    public void PressingDelete_OnASelectedFolder_DeletesNothing_INV081()
    {
        StaThread.Run(() =>
        {
            var panel = BuildDeletablePanel(out var deleted, "Nested/deep.md");
            Row(panel, "Nested").IsSelected = true;

            PressKey(panel, Key.Delete);

            deleted.ShouldBeEmpty();
        });
    }

    [Fact]
    public void PressingDelete_WithNothingSelected_DeletesNothing_INV081()
    {
        StaThread.Run(() =>
        {
            var panel = BuildDeletablePanel(out var deleted, "top.md");

            PressKey(panel, Key.Delete);

            deleted.ShouldBeEmpty();
        });
    }

    [Fact]
    public void OpeningTheContextMenu_OnAFile_OffersItAndSelectsThatFile_INV081()
    {
        StaThread.Run(() =>
        {
            var panel = BuildDeletablePanel(out var deleted, "top.md", "Nested/deep.md");
            var row = Row(panel, "Nested", "deep.md");

            var offered = panel.PrepareContextMenuAt(HeaderTextOf(row));

            // The row right-clicked becomes the Selected Folder Entry, so the menu's Delete acts on
            // exactly the File the user pointed at, and the highlight shows which one that is.
            offered.ShouldBeTrue();
            panel.SelectedEntry?.RelativePath.ShouldBe("Nested/deep.md");
            deleted.ShouldBeEmpty(); // opening the menu is not deleting
        });
    }

    [Fact]
    public void OpeningTheContextMenu_OnAFolder_OffersNothing_INV081()
    {
        StaThread.Run(() =>
        {
            var panel = BuildDeletablePanel(out _, "Nested/deep.md");
            var row = Row(panel, "Nested");

            panel.PrepareContextMenuAt(HeaderTextOf(row)).ShouldBeFalse();
            panel.SelectedEntry.ShouldBeNull();
        });
    }

    [Fact]
    public void Rebuilding_TheTree_KeepsAnExpandedFolderExpanded_INV044()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "Nested/a.md", "Nested/b.md", "top.md");
            Row(panel, "Nested"); // the user Expands it

            // A file inside it is deleted; the Folder Tree is rebuilt with brand-new rows.
            Rebuild(panel, "Nested/a.md", "top.md");

            ExistingRow(panel, "Nested").IsExpanded.ShouldBeTrue();
        });
    }

    [Fact]
    public void Rebuilding_TheTree_KeepsANestedExpandedFolderExpanded_INV044()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "Outer/Inner/a.md", "Outer/Inner/b.md");
            Row(panel, "Outer", "Inner");

            Rebuild(panel, "Outer/Inner/a.md");

            ExistingRow(panel, "Outer").IsExpanded.ShouldBeTrue();
            ExistingRow(panel, "Outer", "Inner").IsExpanded.ShouldBeTrue();
        });
    }

    [Fact]
    public void Rebuilding_TheTree_KeepsACollapsedFolderCollapsed_INV044()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "Nested/a.md", "Nested/b.md");
            Row(panel, "Nested").IsExpanded = false; // Expanded, then Collapsed again

            Rebuild(panel, "Nested/a.md");

            ExistingRow(panel, "Nested").IsExpanded.ShouldBeFalse();
        });
    }

    [Fact]
    public void OpeningADifferentRoot_StartsWithEveryFolderCollapsed_INV044()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "Nested/a.md");
            Row(panel, "Nested");

            panel.Workspace = FolderWorkspace.From(@"C:\elsewhere", ["Nested/a.md"]);
            Layout(panel);

            ExistingRow(panel, "Nested").IsExpanded.ShouldBeFalse();
        });
    }

    [Fact]
    public void Rebuilding_TheTree_KeepsTheSameRowForAnEntryThatDidNotChange_INV044()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "Nested/a.md", "Nested/b.md", "top.md");
            var before = Row(panel, "top.md");

            Rebuild(panel, "Nested/a.md", "top.md");

            // Only what changed on disk is re-created; everything else stays exactly as the user left it.
            ExistingRow(panel, "top.md").ShouldBeSameAs(before);
        });
    }

    [Fact]
    public void Rebuilding_TheTree_KeepsTheHighlightedRowHighlighted_INV044()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "Nested/a.md", "Nested/b.md");
            Row(panel, "Nested", "b.md").IsSelected = true;

            Rebuild(panel, "Nested/b.md", "Nested/c.md");

            ExistingRow(panel, "Nested", "b.md").IsSelected.ShouldBeTrue();
            panel.SelectedEntry?.RelativePath.ShouldBe("Nested/b.md");
        });
    }

    [Fact]
    public void Rebuilding_TheTree_KeepsThePanelScrolledWhereItWas_INV044()
    {
        StaThread.Run(() =>
        {
            var paths = Enumerable.Range(1, 30)
                .SelectMany(i => new[] { $"A/a{i:00}.md", $"B/b{i:00}.md", $"C/c{i:00}.md" })
                .ToArray();
            var panel = BuildPanel(out _, paths);
            Row(panel, "A");
            Row(panel, "B");
            Row(panel, "C");
            var viewer = ScrollViewerOf(panel);
            viewer.ScrollToVerticalOffset(viewer.ScrollableHeight / 2);
            Layout(panel);
            var offset = viewer.VerticalOffset;
            offset.ShouldBeGreaterThan(0);

            Rebuild(panel, [.. paths, "C/c99.md"]);

            viewer.VerticalOffset.ShouldBe(offset);
        });
    }

    [Theory]
    [InlineData(new[] { "a.md", "c.md" }, new[] { "a.md", "b.md", "c.md" })]
    [InlineData(new[] { "a.md", "b.md", "c.md" }, new[] { "a.md", "c.md" })]
    [InlineData(new[] { "Sub/x.md", "top.md" }, new[] { "New/y.md", "Sub/x.md", "top.md" })]
    [InlineData(new[] { "Sub/x.md", "top.md" }, new[] { "top.md" })]
    [InlineData(new[] { "Sub/old.md", "b.md" }, new[] { "Sub/new.md", "a.md" })]
    public void Rebuilding_TheTree_ListsExactlyTheNewFolderTree_INV044(string[] before, string[] after)
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, before);

            Rebuild(panel, after);

            Listed(panel.Items).ShouldBe(Listed(FolderWorkspace.From(Root, after).Entries));
        });
    }
}
