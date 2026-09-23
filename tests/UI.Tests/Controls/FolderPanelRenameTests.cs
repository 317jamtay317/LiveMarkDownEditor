using System.Windows.Controls;
using System.Windows.Input;
using Domain;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;
using static UI.Tests.Controls.FolderPanelHarness;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for Rename File in the <see cref="FolderPanel"/> (INV-082): F2 or the context menu edits a
/// File's name in place, never a Folder's; Enter or focus leaving the name editor commits the typed New
/// Name, while Escape and an unchanged name commit nothing; keys and clicks inside the name editor
/// neither open nor delete the File; and the renamed File's row is highlighted once the rebuilt Folder
/// Tree holds it.
/// </summary>
public sealed class FolderPanelRenameTests
{
    [Fact]
    public void PressingF2_OnASelectedFile_StartsEditingItsName_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out var renamed, "top.md", "Nested/deep.md");
            var row = Row(panel, "Nested", "deep.md");
            row.IsSelected = true;

            PressKey(panel, Key.F2);

            RowOf(row).IsRenaming.ShouldBeTrue();
            RowOf(row).NewName.ShouldBe("deep.md");
            renamed.ShouldBeEmpty(); // starting to edit is not renaming
        });
    }

    [Fact]
    public void PressingF2_OnASelectedFolder_EditsNothing_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out _, "Nested/deep.md");
            var row = Row(panel, "Nested");
            row.IsSelected = true;

            PressKey(panel, Key.F2);

            RowOf(row).IsRenaming.ShouldBeFalse();
        });
    }

    [Fact]
    public void PressingF2_WithNothingSelected_EditsNothing_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out _, "top.md");

            PressKey(panel, Key.F2);

            RowOf(Row(panel, "top.md")).IsRenaming.ShouldBeFalse();
        });
    }

    [Fact]
    public void RenameFile_OnTheContextMenu_StartsEditingTheFileRightClicked_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out _, "top.md", "Nested/deep.md");
            var row = Row(panel, "Nested", "deep.md");
            panel.PrepareContextMenuAt(HeaderTextOf(row));

            FolderPanel.EditFileName.CanExecute(null, panel).ShouldBeTrue();
            FolderPanel.EditFileName.Execute(null, panel);

            RowOf(row).IsRenaming.ShouldBeTrue();
        });
    }

    [Fact]
    public void RenameFile_IsNotOffered_ForAFolder_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out _, "Nested/deep.md");
            Row(panel, "Nested").IsSelected = true;

            FolderPanel.EditFileName.CanExecute(null, panel).ShouldBeFalse();
        });
    }

    [Fact]
    public void PressingEnter_WhileEditing_RenamesTheFileToTheTypedName_AndOpensNothing_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out var renamed, out var activated, "Nested/deep.md");
            var row = StartEditing(panel, "Nested", "deep.md");
            RowOf(row).NewName = "ideas";

            PressKey(panel, Key.Enter);

            renamed.Select(request => (request.File.RelativePath, request.NewName)).ShouldBe([("Nested/deep.md", "ideas")]);
            RowOf(row).IsRenaming.ShouldBeFalse();
            activated.ShouldBeEmpty(); // Enter finishes the name; it does not open the File
        });
    }

    [Fact]
    public void PressingEscape_WhileEditing_RenamesNothing_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out var renamed, "top.md");
            var row = StartEditing(panel, "top.md");
            RowOf(row).NewName = "ideas";

            PressKey(panel, Key.Escape);

            renamed.ShouldBeEmpty();
            RowOf(row).IsRenaming.ShouldBeFalse();
        });
    }

    [Fact]
    public void PressingEnter_WithTheNameUnchanged_RenamesNothing_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out var renamed, "top.md");
            var row = StartEditing(panel, "top.md");

            PressKey(panel, Key.Enter);

            renamed.ShouldBeEmpty();
            RowOf(row).IsRenaming.ShouldBeFalse();
        });
    }

    [Fact]
    public void PressingDelete_WhileEditing_DeletesNothing_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out _, "top.md");
            var deleted = new List<FolderEntry>();
            panel.DeleteCommand = new RecordingCommand(deleted);
            var row = StartEditing(panel, "top.md");

            PressKey(panel, Key.Delete);

            // Delete belongs to the name being typed, not to the File.
            deleted.ShouldBeEmpty();
            RowOf(row).IsRenaming.ShouldBeTrue();
        });
    }

    [Fact]
    public void FocusLeavingTheNameEditor_RenamesTheFileToTheTypedName_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out var renamed, "top.md");
            var row = StartEditing(panel, "top.md");
            RowOf(row).NewName = "ideas.md";

            LoseFocus(NameEditorOf(row), newFocus: null);

            renamed.Select(request => request.NewName).ShouldBe(["ideas.md"]);
            RowOf(row).IsRenaming.ShouldBeFalse();
        });
    }

    [Fact]
    public void FocusMovingIntoTheNameEditorsOwnContextMenu_KeepsEditing_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out var renamed, "top.md");
            var row = StartEditing(panel, "top.md");
            var menu = new ContextMenu();
            var paste = new MenuItem();
            menu.Items.Add(paste);

            LoseFocus(NameEditorOf(row), newFocus: paste);

            renamed.ShouldBeEmpty();
            RowOf(row).IsRenaming.ShouldBeTrue();
        });
    }

    [Fact]
    public void DoubleClicking_InsideTheNameEditor_OpensNothing_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out _, out var activated, "top.md");
            var row = StartEditing(panel, "top.md");

            // Double-clicking a word in the name selects it; it must not open the File.
            panel.ActivateAt(NameEditorOf(row)).ShouldBeFalse();
            activated.ShouldBeEmpty();
        });
    }

    [Fact]
    public void Rebuilding_AfterARename_HighlightsTheRenamedFile_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out _, "Nested/deep.md", "Nested/other.md", "top.md");
            var row = StartEditing(panel, "Nested", "deep.md");
            RowOf(row).NewName = "zebra";
            PressKey(panel, Key.Enter);

            Rebuild(panel, "Nested/other.md", "Nested/zebra.md", "top.md");

            ExistingRow(panel, "Nested", "zebra.md").IsSelected.ShouldBeTrue();
            panel.SelectedEntry?.RelativePath.ShouldBe("Nested/zebra.md");
        });
    }

    [Fact]
    public void Rebuilding_ThatDoesNotHoldTheRenamedFile_HighlightsNothingLater_INV082()
    {
        StaThread.Run(() =>
        {
            var panel = BuildRenamablePanel(out _, "a.md", "b.md");
            var row = StartEditing(panel, "a.md");
            RowOf(row).NewName = "c";
            PressKey(panel, Key.Enter);

            // The rename was refused, so the next rebuild still has a.md and no c.md.
            Rebuild(panel, "a.md", "b.md");
            Row(panel, "b.md").IsSelected = true;
            Rebuild(panel, "a.md", "b.md", "c.md");

            ExistingRow(panel, "b.md").IsSelected.ShouldBeTrue();
            ExistingRow(panel, "c.md").IsSelected.ShouldBeFalse();
        });
    }
}
