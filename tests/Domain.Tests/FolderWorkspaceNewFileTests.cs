using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for the pure rules of New File on <see cref="FolderWorkspace"/> (INV-086): the new Markdown
/// Document goes in the Save Folder, its Entry Name gains <c>.md</c> when it has no Markdown extension,
/// may be a path, makes a Folder when it ends in a separator, and is refused as New Folder's is.
/// </summary>
public sealed class FolderWorkspaceNewFileTests
{
    private const string Root = @"C:\vault";

    [Fact]
    public void NewFile_WithNothingSelected_IsCreatedInTheRoot_INV086()
    {
        var workspace = FolderWorkspace.From(Root, ["top.md"]);

        var creation = workspace.NewFile(null, "ideas.md");

        creation.Refusal.ShouldBeNull();
        creation.Created!.Kind.ShouldBe(FolderEntryKind.File);
        creation.Created.Name.ShouldBe("ideas.md");
        creation.Created.RelativePath.ShouldBe("ideas.md");
    }

    [Fact]
    public void NewFile_InASelectedFolder_IsCreatedInsideIt_INV086()
    {
        var workspace = FolderWorkspace.From(Root, ["sub/note.md"]);

        workspace.NewFile(Entry(workspace, "sub"), "ideas.md").Created!.RelativePath.ShouldBe("sub/ideas.md");
    }

    [Fact]
    public void NewFile_BesideASelectedFile_IsCreatedInTheFolderHoldingIt_INV086()
    {
        var workspace = FolderWorkspace.From(Root, ["sub/note.md"]);

        workspace.NewFile(Entry(workspace, "sub/note.md"), "ideas.md").Created!.RelativePath.ShouldBe("sub/ideas.md");
    }

    [Theory]
    [InlineData("ideas", "ideas.md")]
    [InlineData("v1.2", "v1.2.md")]
    [InlineData("ideas.markdown", "ideas.markdown")]
    [InlineData("Ideas.MD", "Ideas.MD")]
    [InlineData("  ideas.  ", "ideas.md")]
    public void NewFile_KeepsTheFileAMarkdownDocument_INV086(string typed, string expected)
    {
        var workspace = FolderWorkspace.From(Root, ["top.md"]);

        workspace.NewFile(null, typed).Created!.Name.ShouldBe(expected);
    }

    [Fact]
    public void NewFile_ToAPath_CreatesTheFileWithTheFoldersOnTheWay_INV086()
    {
        var workspace = FolderWorkspace.From(Root, ["top.md"]);

        var creation = workspace.NewFile(null, "drafts/2026/ideas");

        creation.Created!.Kind.ShouldBe(FolderEntryKind.File);
        creation.Created.RelativePath.ShouldBe("drafts/2026/ideas.md");
        creation.Name.ShouldBe("drafts/2026/ideas.md");
    }

    [Theory]
    [InlineData("drafts/")]
    [InlineData(@"drafts\")]
    [InlineData("drafts / ")]
    public void NewFile_ToANameEndingInASeparator_CreatesAFolder_AsVsCodeDoes_INV086(string typed)
    {
        var workspace = FolderWorkspace.From(Root, ["top.md"]);

        var creation = workspace.NewFile(null, typed);

        creation.Created!.Kind.ShouldBe(FolderEntryKind.Folder);
        creation.Created.RelativePath.ShouldBe("drafts");
    }

    [Theory]
    [InlineData("", NameRefusal.Blank)]
    [InlineData("/ideas", NameRefusal.StartsWithSeparator)]
    [InlineData("what?", NameRefusal.InvalidCharacter)]
    [InlineData("CON", NameRefusal.ReservedName)]
    [InlineData("top", NameRefusal.NameTaken)]
    [InlineData("TOP.md", NameRefusal.NameTaken)]
    [InlineData("top.md/inner", NameRefusal.NameTaken)]
    public void NewFile_ToAnEntryNameThatCannotBeUsed_IsRefused_INV086(string typed, NameRefusal refusal)
    {
        var workspace = FolderWorkspace.From(Root, ["top.md"]);

        var creation = workspace.NewFile(null, typed);

        creation.Refusal.ShouldBe(refusal);
        creation.Created.ShouldBeNull();
    }

    private static FolderEntry Entry(FolderWorkspace workspace, string relativePath)
    {
        FolderEntry? Find(IReadOnlyList<FolderEntry> entries) =>
            entries.Select(entry => entry.RelativePath == relativePath ? entry : Find(entry.Children))
                .FirstOrDefault(found => found is not null);

        return Find(workspace.Entries) ?? throw new InvalidOperationException($"No entry at {relativePath}.");
    }
}
