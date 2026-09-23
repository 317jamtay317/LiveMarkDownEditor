using System.IO;
using Domain;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;
using static UI.Tests.Controls.FolderPanelHarness;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for how the <see cref="FolderPanel"/> Follows the Active Session (INV-083): the File pushed
/// into its <see cref="FolderPanel.SelectedEntry"/> is revealed — every Folder above it Expanded and
/// the row brought into view — and highlighted, without activating anything. Nothing pushed leaves no
/// row highlighted.
/// </summary>
public sealed class FolderPanelFollowTests
{
    [Fact]
    public void Following_AFileAtTheRoot_HighlightsItsRow_INV083()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "top.md", "other.md");

            Follow(panel, "top.md");

            ExistingRow(panel, "top.md").IsSelected.ShouldBeTrue();
            ExistingRow(panel, "other.md").IsSelected.ShouldBeFalse();
        });
    }

    [Fact]
    public void Following_ANestedFile_ExpandsTheFoldersAboveIt_AndHighlightsIt_INV083()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "Outer/Inner/buried.md", "top.md");

            Follow(panel, "Outer/Inner/buried.md");

            // ExistingRow finds rows without Expanding anything, so reaching the buried File at all
            // proves the panel opened the Folders above it.
            ExistingRow(panel, "Outer").IsExpanded.ShouldBeTrue();
            ExistingRow(panel, "Outer", "Inner").IsExpanded.ShouldBeTrue();
            ExistingRow(panel, "Outer", "Inner", "buried.md").IsSelected.ShouldBeTrue();
        });
    }

    [Fact]
    public void Following_AFile_ActivatesNothing_INV083()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out var activated, "Nested/deep.md");

            Follow(panel, "Nested/deep.md");

            // Following highlights a row; only a double-click or Enter opens a File (INV-043).
            activated.ShouldBeEmpty();
        });
    }

    [Fact]
    public void Following_NoFile_LeavesNoRowHighlighted_INV083()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "top.md", "other.md");
            Follow(panel, "top.md");

            // An unsaved Tab has no Watched File, so no row is the document on screen.
            panel.SelectedEntry = null;
            Layout(panel);

            panel.SelectedItem.ShouldBeNull();
            ExistingRow(panel, "top.md").IsSelected.ShouldBeFalse();
        });
    }

    [Fact]
    public void Following_AFileTheTreeDoesNotHold_LeavesNoRowHighlighted_INV083()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "top.md");
            Follow(panel, "top.md");

            panel.SelectedEntry = FolderWorkspace.From(Root, ["elsewhere.md"]).Entries[0];
            Layout(panel);

            panel.SelectedItem.ShouldBeNull();
        });
    }

    [Fact]
    public void Following_AFileBelowTheViewport_BringsItsRowIntoView_INV083()
    {
        StaThread.Run(() =>
        {
            var paths = Enumerable.Range(1, 60).Select(number => $"note{number:00}.md").ToArray();
            var panel = BuildPanel(out _, paths);
            ScrollViewerOf(panel).VerticalOffset.ShouldBe(0);

            Follow(panel, "note60.md");

            ScrollViewerOf(panel).VerticalOffset.ShouldBeGreaterThan(0);
        });
    }

    [Fact]
    public void Following_TheFileTheUserAlreadyHighlighted_KeepsItHighlighted_INV083()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "Nested/deep.md", "top.md");
            Row(panel, "Nested", "deep.md").IsSelected = true;

            // The Workspace pushes back the entry the panel itself published; nothing should change.
            Follow(panel, "Nested/deep.md");

            ExistingRow(panel, "Nested", "deep.md").IsSelected.ShouldBeTrue();
            panel.SelectedEntry?.RelativePath.ShouldBe("Nested/deep.md");
        });
    }

    private static void Follow(FolderPanel panel, string relativePath)
    {
        panel.SelectedEntry = FileIn(panel, relativePath);
        Layout(panel);
    }

    private static FolderEntry FileIn(FolderPanel panel, string relativePath) =>
        panel.Workspace!.FileFor(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)))
        ?? throw new InvalidOperationException($"No File '{relativePath}' in the Folder Tree.");
}
