using System.ComponentModel;
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
/// Tests for New Folder in the <see cref="FolderPanel"/> (INV-085): the command opens an empty name
/// editor at the top of the Save Folder, Expanding it; Enter or focus leaving the editor commits the
/// typed Entry Name, while Escape and a blank name commit nothing; a rebuild while typing keeps the
/// editor; and the new Folder is highlighted once the rebuilt Folder Tree holds it. Also the context
/// menu, now offered over any row or empty space, and the Ignored rows the panel dims (INV-084).
/// </summary>
public sealed class FolderPanelNewFolderTests
{
    [Fact]
    public void NewFolder_WithNothingSelected_OpensAnEmptyEditorAtTheTopOfTheRoot_INV085()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out var created, "b.md", "a/x.md");

            StartNewFolder(panel);

            var row = TopRows(panel).First();
            row.IsNewEntry.ShouldBeTrue();
            row.Kind.ShouldBe(FolderEntryKind.Folder);
            row.IsRenaming.ShouldBeTrue();
            row.NewName.ShouldBeEmpty();
            created.ShouldBeEmpty(); // opening the editor is not creating
        });
    }

    [Fact]
    public void NewFolder_WithAFolderSelected_OpensTheEditorInsideIt_ExpandingIt_INV085()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out _, "Nested/deep.md", "top.md");
            var folder = Row(panel, "Nested");
            folder.IsSelected = true;
            RowOf(folder).IsExpanded = false;

            StartNewFolder(panel);

            RowOf(folder).IsExpanded.ShouldBeTrue();
            RowOf(folder).Children[0].IsNewEntry.ShouldBeTrue();
            TopRows(panel).Any(row => row.IsNewEntry).ShouldBeFalse();
        });
    }

    [Fact]
    public void NewFolder_WithAFileSelected_OpensTheEditorInTheFolderHoldingIt_INV085()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out _, "Nested/deep.md", "top.md");
            Row(panel, "Nested", "deep.md").IsSelected = true;

            StartNewFolder(panel);

            RowOf(Row(panel, "Nested")).Children[0].IsNewEntry.ShouldBeTrue();
        });
    }

    [Fact]
    public void PressingEnter_CommitsTheTypedFolderName_ForTheSelectionItBeganWith_INV085()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out var created, "Nested/deep.md");
            Row(panel, "Nested").IsSelected = true;
            StartNewFolder(panel);
            NewEntryRowOf(TopRows(panel))!.NewName = "drafts";

            PressKey(panel, Key.Enter);

            created.Count.ShouldBe(1);
            created[0].Selected!.RelativePath.ShouldBe("Nested");
            created[0].EntryName.ShouldBe("drafts");
            NewEntryRowOf(TopRows(panel)).ShouldBeNull();
        });
    }

    [Fact]
    public void FocusLeavingTheEditor_CommitsTheTypedFolderName_INV085()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out var created, "top.md");
            StartNewFolder(panel);
            var row = TopRows(panel).First();
            row.NewName = "drafts";
            var editor = NameEditorOf((TreeViewItem)panel.ItemContainerGenerator.ContainerFromItem(row));

            LoseFocus(editor, newFocus: null);

            created.Single().EntryName.ShouldBe("drafts");
            NewEntryRowOf(TopRows(panel)).ShouldBeNull();
        });
    }

    [Fact]
    public void PressingEscape_CreatesNothing_AndRemovesTheEditor_INV085()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out var created, "top.md");
            StartNewFolder(panel);
            TopRows(panel).First().NewName = "drafts";

            PressKey(panel, Key.Escape);

            created.ShouldBeEmpty();
            Listed(panel.Items).ShouldBe(["File top.md"]);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" . ")]
    public void CommittingABlankFolderName_CreatesNothing_AndSaysNothing_INV085(string typed)
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out var created, "top.md");
            StartNewFolder(panel);
            TopRows(panel).First().NewName = typed;

            PressKey(panel, Key.Enter);

            created.ShouldBeEmpty();
            Listed(panel.Items).ShouldBe(["File top.md"]);
        });
    }

    [Fact]
    public void CommittingAFolderNameTheRuleRefuses_HandsItOverToBeExplained_INV085()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out var created, "top.md");
            StartNewFolder(panel);
            TopRows(panel).First().NewName = "what?";

            PressKey(panel, Key.Enter);

            created.Single().EntryName.ShouldBe("what?");
        });
    }

    [Fact]
    public void KeysWhileNamingTheFolder_NeitherOpenNorDeleteTheSelectedFile_INV085()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out var created, "top.md");
            var activated = new List<FolderEntry>();
            var deleted = new List<FolderEntry>();
            panel.ActivateCommand = new RecordingCommand(activated);
            panel.DeleteCommand = new RecordingCommand(deleted);
            Row(panel, "top.md").IsSelected = true;
            StartNewFolder(panel);

            PressKey(panel, Key.Delete);

            activated.ShouldBeEmpty();
            deleted.ShouldBeEmpty();
            NewEntryRowOf(TopRows(panel))!.IsRenaming.ShouldBeTrue();
        });
    }

    [Fact]
    public void ARebuildWhileTyping_KeepsTheEditorAndWhatWasTyped_INV085()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out _, "b.md", "a/x.md");
            StartNewFolder(panel);
            var row = TopRows(panel).First();
            row.NewName = "dra";

            Rebuild(panel, "b.md", "a/x.md", "c.md");

            TopRows(panel).First().ShouldBeSameAs(row);
            row.IsRenaming.ShouldBeTrue();
            row.NewName.ShouldBe("dra");
            Listed(TopRows(panel).Skip(1)).ShouldBe(["Folder a", "File a/x.md", "File b.md", "File c.md"]);
        });
    }

    [Fact]
    public void TheNewFolder_IsHighlighted_OnceTheRebuiltTreeHoldsIt_INV085()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out _, "Nested/deep.md");
            Row(panel, "Nested", "deep.md").IsSelected = true;
            StartNewFolder(panel);
            NewEntryRowOf(TopRows(panel))!.NewName = "drafts";
            PressKey(panel, Key.Enter);

            Rebuild(panel, new FolderListing(["Nested/drafts"], ["Nested/deep.md"]));

            panel.SelectedEntry!.RelativePath.ShouldBe("Nested/drafts");
            panel.SelectedEntry.Kind.ShouldBe(FolderEntryKind.Folder);
        });
    }

    [Fact]
    public void NewFolder_IsNotOffered_WithoutANewFolderCommand_OrWhileANameIsBeingEdited_INV085()
    {
        StaThread.Run(() =>
        {
            var bare = BuildPanel(out _, "top.md");
            FolderPanel.CreateFolder.CanExecute(null, bare).ShouldBeFalse();

            var panel = BuildNewFolderPanel(out _, "top.md");
            StartNewFolder(panel);
            FolderPanel.CreateFolder.CanExecute(null, panel).ShouldBeFalse();
        });
    }

    [Fact]
    public void OpeningAFolderWorkspace_AsksForNewFolderToBeReconsidered_SoItsButtonTurnsOn_INV085()
    {
        StaThread.Run(() =>
        {
            // The header's New folder button sits outside the panel, and WPF only asks a routed command
            // again after input. A Folder Workspace restored at startup arrives with no input at all, so
            // without asking, the button stays greyed out while New Folder is available.
            var panel = new FolderPanel { NewFolderCommand = new RecordingNewEntries([]) };
            var asked = 0;
            EventHandler onAsked = (_, _) => asked++;
            FolderPanel.CreateFolder.CanExecuteChanged += onAsked;
            try
            {
                Layout(panel);
                asked = 0;

                panel.Workspace = FolderWorkspace.From(Root, ["top.md"]);
                Layout(panel);

                asked.ShouldBeGreaterThan(0);
            }
            finally
            {
                FolderPanel.CreateFolder.CanExecuteChanged -= onAsked;
            }
        });
    }

    [Fact]
    public void OpeningTheContextMenu_OnAFolder_SelectsIt_AndOffersNoFileAction_INV085()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out _, "Nested/deep.md");
            var row = Row(panel, "Nested");

            panel.PrepareContextMenuAt(HeaderTextOf(row)).ShouldBeTrue();

            panel.SelectedEntry!.RelativePath.ShouldBe("Nested");
            panel.IsFileSelected.ShouldBeFalse();
            FolderPanel.CreateFolder.CanExecute(null, panel).ShouldBeTrue();
        });
    }

    [Fact]
    public void OpeningTheContextMenu_OnAFile_OffersTheFileActions_INV085()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out _, "Nested/deep.md");

            panel.PrepareContextMenuAt(HeaderTextOf(Row(panel, "Nested", "deep.md"))).ShouldBeTrue();

            panel.IsFileSelected.ShouldBeTrue();
        });
    }

    [Fact]
    public void OpeningTheContextMenu_OnEmptySpace_SelectsNothing_SoNewFolderGoesInTheRoot_INV085()
    {
        StaThread.Run(() =>
        {
            var panel = BuildNewFolderPanel(out _, "Nested/deep.md");
            Row(panel, "Nested", "deep.md").IsSelected = true;

            panel.PrepareContextMenuAt(panel).ShouldBeTrue();

            panel.SelectedEntry.ShouldBeNull();
            panel.IsFileSelected.ShouldBeFalse();
            StartNewFolder(panel);
            TopRows(panel).First().IsNewEntry.ShouldBeTrue();
        });
    }

    [Fact]
    public void ARow_IsIgnored_ExactlyWhenItsEntryIs_INV084()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "top.md");

            Rebuild(panel, new FolderListing(["bin", "docs"], ["bin/a.md", "top.md"], ignored: ["bin"]));

            var rows = TopRows(panel).ToList();
            rows.Single(row => row.Name == "bin").IsIgnored.ShouldBeTrue();
            rows.Single(row => row.Name == "bin").Children.Single().IsIgnored.ShouldBeTrue();
            rows.Single(row => row.Name == "docs").IsIgnored.ShouldBeFalse();
            rows.Single(row => row.Name == "top.md").IsIgnored.ShouldBeFalse();
        });
    }

    [Fact]
    public void ARebuild_ThatChangesWhatIsIgnored_UpdatesTheSameRow_INV084()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out _, "top.md");
            Rebuild(panel, new FolderListing(["bin"], ["top.md"]));
            var bin = TopRows(panel).Single(row => row.Name == "bin");
            var changed = new List<string?>();
            ((INotifyPropertyChanged)bin).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            Rebuild(panel, new FolderListing(["bin"], ["top.md"], ignored: ["bin"]));

            TopRows(panel).Single(row => row.Name == "bin").ShouldBeSameAs(bin);
            bin.IsIgnored.ShouldBeTrue();
            changed.ShouldContain(nameof(FolderPanelRow.IsIgnored));
        });
    }
}
