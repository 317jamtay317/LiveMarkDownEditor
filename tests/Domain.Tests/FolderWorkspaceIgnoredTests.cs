using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for what Git ignores in a <see cref="FolderWorkspace"/>'s Folder Tree (INV-084): the Folder
/// Listing's Ignored paths, and everything beneath an Ignored Folder, are marked Ignored — dimmed in the
/// Folder Panel, never left out of the tree.
/// </summary>
public sealed class FolderWorkspaceIgnoredTests
{
    private const string Root = @"C:\vault";

    [Fact]
    public void From_MarksAListedIgnoredFolder_AndEverythingBeneathIt_INV084()
    {
        var listing = new FolderListing(
            ["node_modules", "node_modules/pkg", "notes"],
            ["node_modules/pkg/readme.md", "notes/a.md"],
            ignored: ["node_modules"]);

        var workspace = FolderWorkspace.From(Root, listing);

        Ignored(workspace).ShouldBe(["node_modules", "node_modules/pkg", "node_modules/pkg/readme.md"]);
    }

    [Fact]
    public void From_MarksAListedIgnoredFile_AndNothingBesideIt_INV084()
    {
        var listing = new FolderListing(["notes"], ["notes/scratch.md", "notes/keep.md"], ignored: ["notes/scratch.md"]);

        var workspace = FolderWorkspace.From(Root, listing);

        Ignored(workspace).ShouldBe(["notes/scratch.md"]);
    }

    [Fact]
    public void From_WithNothingIgnored_MarksNothing_INV084()
    {
        var workspace = FolderWorkspace.From(Root, new FolderListing(["bin"], ["bin/a.md", "top.md"]));

        Ignored(workspace).ShouldBeEmpty();
    }

    [Fact]
    public void From_KeepsIgnoredEntries_InTheirPlaceAndOrder_INV084()
    {
        string[] folders = ["bin", "docs"];
        string[] files = ["bin/a.md", "docs/b.md", "top.md"];

        var plain = FolderWorkspace.From(Root, new FolderListing(folders, files));
        var ignored = FolderWorkspace.From(Root, new FolderListing(folders, files, ignored: ["bin", "top.md"]));

        Flatten(ignored).ShouldBe(Flatten(plain));
    }

    [Fact]
    public void WithIgnored_KeepsTheFoldersAndFiles_AndReplacesWhatIsIgnored_INV084()
    {
        var listing = new FolderListing(["bin"], ["bin/a.md"], ignored: ["old"]);

        var withIgnored = listing.WithIgnored(["bin"]);

        withIgnored.Folders.ShouldBe(["bin"]);
        withIgnored.Files.ShouldBe(["bin/a.md"]);
        withIgnored.Ignored.ShouldBe(["bin"], ignoreOrder: true);
    }

    [Fact]
    public void Paths_AreTheFoldersThenTheFiles_INV084()
    {
        new FolderListing(["a", "b"], ["a/x.md"]).Paths.ShouldBe(["a", "b", "a/x.md"]);
    }

    // The relative paths of every Ignored entry, in document order.
    private static IReadOnlyList<string> Ignored(FolderWorkspace workspace)
    {
        var lines = new List<string>();

        void Walk(IReadOnlyList<FolderEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.IsIgnored)
                {
                    lines.Add(entry.RelativePath);
                }

                Walk(entry.Children);
            }
        }

        Walk(workspace.Entries);
        return lines;
    }

    // The tree as "Kind RelativePath" lines in document order.
    private static IReadOnlyList<string> Flatten(FolderWorkspace workspace)
    {
        var lines = new List<string>();

        void Walk(IReadOnlyList<FolderEntry> entries)
        {
            foreach (var entry in entries)
            {
                lines.Add($"{entry.Kind} {entry.RelativePath}");
                Walk(entry.Children);
            }
        }

        Walk(workspace.Entries);
        return lines;
    }
}
