using System.Windows.Input;
using Domain;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;
using static UI.Tests.Controls.FolderPanelHarness;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for New File in the <see cref="FolderPanel"/> (INV-086): the command opens an empty name editor
/// at the top of the Save Folder's files, below its Folders, as VS Code does; Enter commits the typed
/// Entry Name through the NewFileCommand; Escape and a blank name commit nothing; and a New Document
/// request opens the editor (INV-087).
/// </summary>
public sealed class FolderPanelNewFileTests
{
    [Fact]
    public void NewFile_WithNothingSelected_OpensTheEditorBelowTheRootsFolders_INV086()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFilePanel(out var created, "a/x.md", "b/y.md", "top.md");

            StartNewFile(panel);

            var rows = TopRows(panel).ToList();
            rows.Select(row => row.IsNewEntry).ShouldBe([false, false, true, false]);
            rows[2].Kind.ShouldBe(FolderEntryKind.File);
            rows[2].IsRenaming.ShouldBeTrue();
            rows[2].NewName.ShouldBeEmpty();
            created.ShouldBeEmpty();
        });
    }

    [Fact]
    public void NewFile_WithAFolderSelected_OpensTheEditorInsideIt_BelowItsFolders_INV086()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFilePanel(out _, "Nested/inner/x.md", "Nested/deep.md");
            var folder = Row(panel, "Nested");
            folder.IsSelected = true;
            RowOf(folder).IsExpanded = false;

            StartNewFile(panel);

            RowOf(folder).IsExpanded.ShouldBeTrue();
            RowOf(folder).Children.Select(row => row.IsNewEntry).ShouldBe([false, true, false]);
        });
    }

    [Fact]
    public void PressingEnter_CommitsTheTypedEntryName_ToTheNewFileCommand_INV086()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFilePanel(out var created, "Nested/deep.md");
            Row(panel, "Nested", "deep.md").IsSelected = true;
            StartNewFile(panel);
            NewEntryRowOf(TopRows(panel))!.NewName = "ideas";

            PressKey(panel, Key.Enter);

            created.Single().Selected!.RelativePath.ShouldBe("Nested/deep.md");
            created.Single().EntryName.ShouldBe("ideas");
            NewEntryRowOf(TopRows(panel)).ShouldBeNull();
        });
    }

    [Theory]
    [InlineData(Key.Escape, "ideas")]
    [InlineData(Key.Enter, "  ")]
    public void EscapeOrABlankName_CreatesNothing_AndRemovesTheEditor_INV086(Key key, string typed)
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFilePanel(out var created, "top.md");
            StartNewFile(panel);
            NewEntryRowOf(TopRows(panel))!.NewName = typed;

            PressKey(panel, key);

            created.ShouldBeEmpty();
            Listed(panel.Items).ShouldBe(["File top.md"]);
        });
    }

    [Fact]
    public void ARebuildWhileTyping_KeepsTheEditorBelowTheFolders_INV086()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFilePanel(out _, "a/x.md", "top.md");
            StartNewFile(panel);
            var row = NewEntryRowOf(TopRows(panel))!;
            row.NewName = "ide";

            Rebuild(panel, "a/x.md", "top.md", "zed.md");

            TopRows(panel).ElementAt(1).ShouldBeSameAs(row);
            row.NewName.ShouldBe("ide");
            Listed(TopRows(panel).Where(each => !each.IsNewEntry)).ShouldBe(["Folder a", "File a/x.md", "File top.md", "File zed.md"]);
        });
    }

    [Fact]
    public void NewFile_IsNotOffered_WithoutANewFileCommand_INV086()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out _, "top.md");

            FolderPanel.CreateFile.CanExecute(null, panel).ShouldBeFalse();
        });
    }

    [Fact]
    public void ANewFileRequest_OpensTheEditorOnce_AndIsTakenBack_INV087()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFilePanel(out _, "a/x.md", "top.md");

            panel.NewFileRequest = new object();
            Layout(panel);

            panel.NewFileRequest.ShouldBeNull();
            TopRows(panel).Count(row => row.IsNewEntry).ShouldBe(1);
            NewEntryRowOf(TopRows(panel))!.Kind.ShouldBe(FolderEntryKind.File);
        });
    }
}
