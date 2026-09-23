using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for the pure rules of Rename File on <see cref="FolderWorkspace"/> (INV-082): only a File the
/// Folder Tree holds can be renamed, and a New Name is tidied, stays a Markdown Document in the File's
/// own folder, and is refused when it cannot be used.
/// </summary>
public sealed class FolderWorkspaceRenameTests
{
    private const string Root = @"C:\vault";

    [Fact]
    public void CanRename_AFileInTheTree_IsTrue_INV082()
    {
        var workspace = FolderWorkspace.From(Root, ["top.md"]);

        workspace.CanRename(workspace.Entries[0]).ShouldBeTrue();
    }

    [Fact]
    public void CanRename_AFileNestedInAFolder_IsTrue_INV082()
    {
        var workspace = FolderWorkspace.From(Root, ["a/b/deep.md"]);

        workspace.CanRename(Entry(workspace, "a/b/deep.md")).ShouldBeTrue();
    }

    [Fact]
    public void CanRename_AFolder_IsFalse_INV082()
    {
        var workspace = FolderWorkspace.From(Root, ["sub/note.md"]);

        workspace.CanRename(Entry(workspace, "sub")).ShouldBeFalse();
    }

    [Fact]
    public void CanRename_AFileTheTreeNoLongerHolds_IsFalse_INV082()
    {
        var before = FolderWorkspace.From(Root, ["gone.md", "kept.md"]);
        var after = FolderWorkspace.From(Root, ["kept.md"]);

        after.CanRename(Entry(before, "gone.md")).ShouldBeFalse();
    }

    [Fact]
    public void CanRename_Nothing_IsFalse_INV082()
    {
        FolderWorkspace.From(Root, ["top.md"]).CanRename(null).ShouldBeFalse();
    }

    [Fact]
    public void Rename_GivesTheFileItsNewName_InItsOwnFolder_INV082()
    {
        var workspace = FolderWorkspace.From(Root, ["sub/note.md"]);
        var file = Entry(workspace, "sub/note.md");

        var rename = workspace.Rename(file, "ideas.md");

        rename.File.ShouldBe(file);
        rename.Refusal.ShouldBeNull();
        rename.IsUnchanged.ShouldBeFalse();
        rename.NewName.ShouldBe("ideas.md");
        rename.Renamed.ShouldBe(new FolderEntry(FolderEntryKind.File, "ideas.md", "sub/ideas.md", []));
    }

    [Fact]
    public void Rename_AFileAtTheRoot_StaysAtTheRoot_INV082()
    {
        var workspace = FolderWorkspace.From(Root, ["note.md"]);

        workspace.Rename(Entry(workspace, "note.md"), "ideas.md").Renamed!.RelativePath.ShouldBe("ideas.md");
    }

    [Theory]
    [InlineData("Ideas", "Ideas.md")]
    [InlineData("v1.2", "v1.2.md")]
    [InlineData("notes.txt", "notes.txt.md")]
    [InlineData("Ideas.markdown", "Ideas.markdown")]
    [InlineData("Ideas.MD", "Ideas.MD")]
    public void Rename_ANewNameWithoutAMarkdownExtension_KeepsTheFilesOwn_INV082(string typed, string expected)
    {
        var workspace = FolderWorkspace.From(Root, ["note.md"]);

        // A renamed File stays a Markdown Document, so it never drops out of the Folder Tree.
        workspace.Rename(Entry(workspace, "note.md"), typed).NewName.ShouldBe(expected);
    }

    [Fact]
    public void Rename_KeepsTheFilesOwnExtension_AsItIsWritten_INV082()
    {
        var workspace = FolderWorkspace.From(Root, ["Readme.markdown"]);

        workspace.Rename(Entry(workspace, "Readme.markdown"), "Guide").NewName.ShouldBe("Guide.markdown");
    }

    [Theory]
    [InlineData("  ideas.md  ", "ideas.md")]
    [InlineData("ideas.", "ideas.md")]
    [InlineData("ideas. . ", "ideas.md")]
    [InlineData(".hidden.md", ".hidden.md")]
    public void Rename_TidiesTheNewName_AsWindowsWould_INV082(string typed, string expected)
    {
        var workspace = FolderWorkspace.From(Root, ["note.md"]);

        workspace.Rename(Entry(workspace, "note.md"), typed).NewName.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData(null)]
    public void Rename_ToABlankName_IsRefused_INV082(string? typed)
    {
        var workspace = FolderWorkspace.From(Root, ["note.md"]);

        var rename = workspace.Rename(Entry(workspace, "note.md"), typed);

        rename.Refusal.ShouldBe(RenameRefusal.Blank);
        rename.Renamed.ShouldBeNull();
    }

    [Theory]
    [InlineData("sub/ideas.md")]
    [InlineData(@"..\ideas.md")]
    [InlineData("what?.md")]
    [InlineData("a:b.md")]
    [InlineData("star*.md")]
    [InlineData("\"quoted\".md")]
    [InlineData("<angle>.md")]
    [InlineData("pipe|.md")]
    [InlineData("tab\there.md")]
    public void Rename_ToANameWithACharacterWindowsForbids_IsRefused_INV082(string typed)
    {
        var workspace = FolderWorkspace.From(Root, ["sub/note.md"]);

        // Refusing a path separator is also what keeps the File in its own folder.
        workspace.Rename(Entry(workspace, "sub/note.md"), typed).Refusal.ShouldBe(RenameRefusal.InvalidCharacter);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con.md")]
    [InlineData("Prn.md")]
    [InlineData("aux")]
    [InlineData("NUL.markdown")]
    [InlineData("COM1")]
    [InlineData("com9.md")]
    [InlineData("LPT1.md")]
    [InlineData("lpt9")]
    [InlineData("CON.backup.md")]
    public void Rename_ToANameWindowsReserves_IsRefused_INV082(string typed)
    {
        var workspace = FolderWorkspace.From(Root, ["note.md"]);

        workspace.Rename(Entry(workspace, "note.md"), typed).Refusal.ShouldBe(RenameRefusal.ReservedName);
    }

    [Theory]
    [InlineData("console.md")]
    [InlineData("COM10.md")]
    [InlineData("LPT.md")]
    [InlineData("my CON.md")]
    public void Rename_ToANameThatOnlyLooksReserved_IsAllowed_INV082(string typed)
    {
        var workspace = FolderWorkspace.From(Root, ["note.md"]);

        workspace.Rename(Entry(workspace, "note.md"), typed).Refusal.ShouldBeNull();
    }

    [Theory]
    [InlineData("other.md")]
    [InlineData("OTHER.md")]
    [InlineData("other")]
    public void Rename_ToTheNameOfAnotherFileInTheSameFolder_IsRefused_INV082(string typed)
    {
        var workspace = FolderWorkspace.From(Root, ["sub/note.md", "sub/other.md"]);

        var rename = workspace.Rename(Entry(workspace, "sub/note.md"), typed);

        // Windows compares names without regard to capitals, so a rename must never overwrite.
        rename.Refusal.ShouldBe(RenameRefusal.NameTaken);
        rename.Renamed.ShouldBeNull();
    }

    [Fact]
    public void Rename_ToTheNameOfAFolderInTheSameFolder_IsRefused_INV082()
    {
        var workspace = FolderWorkspace.From(Root, ["archive.md/old.md", "note.md"]);

        workspace.Rename(Entry(workspace, "note.md"), "archive.md").Refusal.ShouldBe(RenameRefusal.NameTaken);
    }

    [Fact]
    public void Rename_ToTheNameOfAFileInAnotherFolder_IsAllowed_INV082()
    {
        var workspace = FolderWorkspace.From(Root, ["sub/note.md", "other.md", "elsewhere/other.md"]);

        workspace.Rename(Entry(workspace, "sub/note.md"), "other.md").Refusal.ShouldBeNull();
    }

    [Fact]
    public void Rename_ChangingOnlyTheCapitals_IsAllowed_INV082()
    {
        var workspace = FolderWorkspace.From(Root, ["sub/note.md"]);

        var rename = workspace.Rename(Entry(workspace, "sub/note.md"), "Note.md");

        rename.Refusal.ShouldBeNull();
        rename.IsUnchanged.ShouldBeFalse();
        rename.Renamed!.RelativePath.ShouldBe("sub/Note.md");
    }

    [Theory]
    [InlineData("note.md")]
    [InlineData(" note.md ")]
    [InlineData("note")]
    public void Rename_ToTheFilesOwnName_IsUnchanged_INV082(string typed)
    {
        var workspace = FolderWorkspace.From(Root, ["note.md"]);

        var rename = workspace.Rename(Entry(workspace, "note.md"), typed);

        rename.IsUnchanged.ShouldBeTrue();
        rename.Refusal.ShouldBeNull();
    }

    [Fact]
    public void Rename_AFolder_Throws_INV082()
    {
        var workspace = FolderWorkspace.From(Root, ["sub/note.md"]);

        Should.Throw<ArgumentException>(() => workspace.Rename(Entry(workspace, "sub"), "renamed"));
    }

    [Fact]
    public void Rename_AFileTheTreeNoLongerHolds_Throws_INV082()
    {
        var before = FolderWorkspace.From(Root, ["gone.md", "kept.md"]);
        var after = FolderWorkspace.From(Root, ["kept.md"]);

        Should.Throw<ArgumentException>(() => after.Rename(Entry(before, "gone.md"), "renamed"));
    }

    private static FolderEntry Entry(FolderWorkspace workspace, string relativePath)
    {
        FolderEntry? Find(IReadOnlyList<FolderEntry> entries) =>
            entries.FirstOrDefault(entry => entry.RelativePath == relativePath)
            ?? entries.Select(entry => Find(entry.Children)).FirstOrDefault(found => found is not null);

        return Find(workspace.Entries)
               ?? throw new InvalidOperationException($"No entry '{relativePath}' in the tree.");
    }
}
