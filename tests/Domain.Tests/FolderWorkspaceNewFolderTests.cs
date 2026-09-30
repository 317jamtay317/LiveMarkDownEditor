using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for the pure rules of New Folder on <see cref="FolderWorkspace"/> (INV-085): the new Folder
/// goes in the Save Folder, and an Entry Name is tidied, gains no extension, and is refused when it
/// cannot be used.
/// </summary>
public sealed class FolderWorkspaceNewFolderTests
{
    private const string Root = @"C:\vault";

    [Fact]
    public void SaveFolderEntryFor_NothingSelected_IsTheRoot_INV085()
    {
        FolderWorkspace.From(Root, ["sub/note.md"]).SaveFolderEntryFor(null).ShouldBeNull();
    }

    [Fact]
    public void SaveFolderEntryFor_ASelectedFolder_IsThatFolder_INV085()
    {
        var workspace = FolderWorkspace.From(Root, ["a/b/note.md"]);

        workspace.SaveFolderEntryFor(Entry(workspace, "a/b"))!.RelativePath.ShouldBe("a/b");
    }

    [Fact]
    public void SaveFolderEntryFor_ASelectedFile_IsTheFolderHoldingIt_INV085()
    {
        var workspace = FolderWorkspace.From(Root, ["a/b/note.md"]);

        var folder = workspace.SaveFolderEntryFor(Entry(workspace, "a/b/note.md"));

        folder!.Kind.ShouldBe(FolderEntryKind.Folder);
        folder.RelativePath.ShouldBe("a/b");
    }

    [Fact]
    public void SaveFolderEntryFor_ASelectedRootLevelFile_IsTheRoot_INV085()
    {
        var workspace = FolderWorkspace.From(Root, ["top.md"]);

        workspace.SaveFolderEntryFor(Entry(workspace, "top.md")).ShouldBeNull();
    }

    [Fact]
    public void SaveFolderEntryFor_AnEntryTheTreeNoLongerHolds_IsTheRoot_INV085()
    {
        var before = FolderWorkspace.From(Root, ["gone/note.md", "kept.md"]);
        var after = FolderWorkspace.From(Root, ["kept.md"]);

        after.SaveFolderEntryFor(Entry(before, "gone")).ShouldBeNull();
    }

    [Fact]
    public void NewFolder_WithNothingSelected_IsCreatedInTheRoot_INV085()
    {
        var workspace = FolderWorkspace.From(Root, ["top.md"]);

        var creation = workspace.NewFolder(null, "drafts");

        creation.Refusal.ShouldBeNull();
        creation.Created!.Kind.ShouldBe(FolderEntryKind.Folder);
        creation.Created.Name.ShouldBe("drafts");
        creation.Created.RelativePath.ShouldBe("drafts");
        creation.Created.Children.ShouldBeEmpty();
    }

    [Fact]
    public void NewFolder_InASelectedFolder_IsCreatedInsideIt_INV085()
    {
        var workspace = FolderWorkspace.From(Root, ["sub/note.md"]);

        workspace.NewFolder(Entry(workspace, "sub"), "drafts").Created!.RelativePath.ShouldBe("sub/drafts");
    }

    [Fact]
    public void NewFolder_BesideASelectedFile_IsCreatedInTheFolderHoldingIt_INV085()
    {
        var workspace = FolderWorkspace.From(Root, ["sub/note.md"]);

        workspace.NewFolder(Entry(workspace, "sub/note.md"), "drafts").Created!.RelativePath.ShouldBe("sub/drafts");
    }

    [Fact]
    public void NewFolder_ForAnEntryTheTreeNoLongerHolds_IsCreatedInTheRoot_INV085()
    {
        var before = FolderWorkspace.From(Root, ["gone/note.md", "kept.md"]);
        var after = FolderWorkspace.From(Root, ["kept.md"]);

        after.NewFolder(Entry(before, "gone"), "drafts").Created!.RelativePath.ShouldBe("drafts");
    }

    [Theory]
    [InlineData("  drafts  ", "drafts")]
    [InlineData("drafts.", "drafts")]
    [InlineData("drafts . . ", "drafts")]
    [InlineData("v1.2", "v1.2")]
    [InlineData("notes", "notes")]
    public void NewFolder_TidiesTheEntryName_AndAddsNoExtension_INV085(string typed, string expected)
    {
        var workspace = FolderWorkspace.From(Root, ["top.md"]);

        var creation = workspace.NewFolder(null, typed);

        creation.Name.ShouldBe(expected);
        creation.Created!.Name.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, NameRefusal.Blank)]
    [InlineData("", NameRefusal.Blank)]
    [InlineData("   ", NameRefusal.Blank)]
    [InlineData("...", NameRefusal.Blank)]
    [InlineData("a/ /b", NameRefusal.Blank)]
    [InlineData("a/../b", NameRefusal.Blank)]
    [InlineData("/drafts", NameRefusal.StartsWithSeparator)]
    [InlineData(@"\drafts", NameRefusal.StartsWithSeparator)]
    [InlineData("  /drafts", NameRefusal.StartsWithSeparator)]
    [InlineData("what?", NameRefusal.InvalidCharacter)]
    [InlineData("ok/what?", NameRefusal.InvalidCharacter)]
    [InlineData("a:b", NameRefusal.InvalidCharacter)]
    [InlineData("tab\there", NameRefusal.InvalidCharacter)]
    [InlineData("CON", NameRefusal.ReservedName)]
    [InlineData("lpt1", NameRefusal.ReservedName)]
    [InlineData("nul.txt", NameRefusal.ReservedName)]
    [InlineData("ok/con", NameRefusal.ReservedName)]
    public void NewFolder_ToAnEntryNameThatCannotBeUsed_IsRefused_INV085(string? typed, NameRefusal refusal)
    {
        var workspace = FolderWorkspace.From(Root, ["top.md"]);

        var creation = workspace.NewFolder(null, typed);

        creation.Refusal.ShouldBe(refusal);
        creation.Created.ShouldBeNull();
    }

    [Theory]
    [InlineData("archive")]
    [InlineData("ARCHIVE")]
    [InlineData("note.md")]
    [InlineData("Note.MD")]
    public void NewFolder_ToANameAnotherEntryInTheSaveFolderHas_IsRefusedAsTaken_INV085(string typed)
    {
        var workspace = FolderWorkspace.From(Root, ["sub/archive/old.md", "sub/note.md"]);

        var creation = workspace.NewFolder(Entry(workspace, "sub"), typed);

        creation.Refusal.ShouldBe(NameRefusal.NameTaken);
        creation.Created.ShouldBeNull();
    }

    [Fact]
    public void NewFolder_ToANameOnlyAnotherFolderHas_IsNotTaken_INV085()
    {
        var workspace = FolderWorkspace.From(Root, ["elsewhere/archive/old.md", "sub/note.md"]);

        workspace.NewFolder(Entry(workspace, "sub"), "archive").Refusal.ShouldBeNull();
    }

    [Fact]
    public void NewFolder_ToTheNameOfAnEmptyFolderBesideIt_IsTaken_INV085()
    {
        var workspace = FolderWorkspace.From(Root, new FolderListing(["drafts"], []));

        workspace.NewFolder(null, "Drafts").Refusal.ShouldBe(NameRefusal.NameTaken);
    }

    [Theory]
    [InlineData("drafts/2026", "drafts/2026", "2026")]
    [InlineData(@"drafts\2026", "drafts/2026", "2026")]
    [InlineData(" drafts / 2026. /", "drafts/2026", "2026")]
    public void NewFolder_ToAPath_CreatesItsLastFolder_AndTheFoldersOnTheWay_INV085(
        string typed, string relativePath, string name)
    {
        var workspace = FolderWorkspace.From(Root, ["top.md"]);

        var creation = workspace.NewFolder(null, typed);

        creation.Refusal.ShouldBeNull();
        creation.Name.ShouldBe(relativePath);
        creation.Created!.RelativePath.ShouldBe(relativePath);
        creation.Created.Name.ShouldBe(name);
        creation.Created.Kind.ShouldBe(FolderEntryKind.Folder);
    }

    [Fact]
    public void NewFolder_ToAPathThroughAFolderThatIsThere_CreatesInsideIt_INV085()
    {
        var workspace = FolderWorkspace.From(Root, ["sub/archive/old.md"]);

        workspace.NewFolder(Entry(workspace, "sub"), "Archive/2026").Created!.RelativePath.ShouldBe("sub/Archive/2026");
    }

    [Fact]
    public void NewFolder_ToAPathTheTreeAlreadyHolds_IsRefusedAsTaken_INV085()
    {
        var workspace = FolderWorkspace.From(Root, ["sub/archive/old.md"]);

        workspace.NewFolder(null, "SUB/archive").Refusal.ShouldBe(NameRefusal.NameTaken);
    }

    [Fact]
    public void NewFolder_ToAPathThroughAFile_IsRefusedAsTaken_INV085()
    {
        var workspace = FolderWorkspace.From(Root, ["note.md"]);

        workspace.NewFolder(null, "note.md/inner").Refusal.ShouldBe(NameRefusal.NameTaken);
    }

    private static FolderEntry Entry(FolderWorkspace workspace, string relativePath)
    {
        FolderEntry? Find(IReadOnlyList<FolderEntry> entries) =>
            entries.Select(entry => entry.RelativePath == relativePath ? entry : Find(entry.Children))
                .FirstOrDefault(found => found is not null);

        return Find(workspace.Entries) ?? throw new InvalidOperationException($"No entry at {relativePath}.");
    }
}
