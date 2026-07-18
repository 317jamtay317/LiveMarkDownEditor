using System.IO;
using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for <see cref="FolderWorkspace"/> and <see cref="FolderEntry"/> — the pruned, ordered,
/// Markdown-only Folder Tree a Folder Workspace presents (INV-042). The builder is pure: it takes the
/// root and the set of root-relative, <c>/</c>-separated file paths beneath it and yields a
/// deterministic tree.
/// </summary>
public sealed class FolderWorkspaceTests
{
    private const string Root = @"C:\vault";

    /// <summary>
    /// Flattens the Folder Tree to "<c>Kind RelativePath</c>" lines in document order, so a test can
    /// assert the whole structure — kinds, relative paths, and ordering — in one comparison.
    /// </summary>
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

    [Fact]
    public void From_KeepsOnlyMarkdownFiles_INV042()
    {
        var workspace = FolderWorkspace.From(Root, ["a.md", "b.txt", "c.markdown", "d.mdx", "e.md.bak", "readme"]);

        Flatten(workspace).ShouldBe(["File a.md", "File c.markdown"]);
    }

    [Theory]
    [InlineData("A.MD")]
    [InlineData("B.Markdown")]
    [InlineData("C.MARKDOWN")]
    public void From_MatchesTheMarkdownExtension_CaseInsensitively_INV042(string name)
    {
        var workspace = FolderWorkspace.From(Root, [name]);

        workspace.Entries.Count.ShouldBe(1);
        workspace.Entries[0].Kind.ShouldBe(FolderEntryKind.File);
        workspace.Entries[0].Name.ShouldBe(name);
    }

    [Fact]
    public void From_OrdersFiles_CaseInsensitively_INV042()
    {
        var workspace = FolderWorkspace.From(Root, ["banana.md", "Apple.md", "cherry.md"]);

        Flatten(workspace).ShouldBe(["File Apple.md", "File banana.md", "File cherry.md"]);
    }

    [Fact]
    public void From_PutsFoldersBeforeFiles_INV042()
    {
        var workspace = FolderWorkspace.From(Root, ["zebra.md", "alpha/one.md"]);

        Flatten(workspace).ShouldBe(["Folder alpha", "File alpha/one.md", "File zebra.md"]);
    }

    [Fact]
    public void From_IsDeterministic_RegardlessOfInputOrder_INV042()
    {
        string[] paths = ["gamma/two.md", "alpha/one.md", "beta.md", "alpha/three.md", "top.md"];
        var forward = FolderWorkspace.From(Root, paths);
        var reversed = FolderWorkspace.From(Root, paths.Reverse());

        Flatten(reversed).ShouldBe(Flatten(forward));
    }

    [Fact]
    public void From_PrunesFoldersWithNoMarkdownBeneath_INV042()
    {
        var workspace = FolderWorkspace.From(Root, ["docs/readme.txt", "assets/logo.png", "notes/a.md"]);

        Flatten(workspace).ShouldBe(["Folder notes", "File notes/a.md"]);
    }

    [Fact]
    public void From_KeepsADeepChainLeadingToMarkdown_INV042()
    {
        var workspace = FolderWorkspace.From(Root, ["a/b/c/d/note.md"]);

        Flatten(workspace).ShouldBe(
        [
            "Folder a", "Folder a/b", "Folder a/b/c", "Folder a/b/c/d", "File a/b/c/d/note.md",
        ]);
    }

    [Fact]
    public void From_KeepsSharedPrefixesDistinct_INV042()
    {
        // "a" and "ab" share a leading letter but are different folders and must not merge.
        var workspace = FolderWorkspace.From(Root, ["a/b.md", "ab/c.md"]);

        Flatten(workspace).ShouldBe(["Folder a", "File a/b.md", "Folder ab", "File ab/c.md"]);
    }

    [Fact]
    public void From_SharesCommonAncestors_INV042()
    {
        var workspace = FolderWorkspace.From(Root, ["a/b/c.md", "a/b/d.md"]);

        Flatten(workspace).ShouldBe(["Folder a", "Folder a/b", "File a/b/c.md", "File a/b/d.md"]);
    }

    [Fact]
    public void From_TreatsAFolderAndAFileWithTheSameBaseName_AsDistinctSiblings_INV042()
    {
        // The folder "notes" (a directory literally beside a file "notes.md") and the file sort as
        // distinct siblings, folder first.
        var workspace = FolderWorkspace.From(Root, ["notes.md", "notes/inner.md"]);

        Flatten(workspace).ShouldBe(["Folder notes", "File notes/inner.md", "File notes.md"]);
    }

    [Fact]
    public void From_GivenNoMarkdown_HasNoEntries_INV042()
    {
        var workspace = FolderWorkspace.From(Root, ["readme.txt", "img/logo.png"]);

        workspace.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void From_GivenNoPaths_HasNoEntries_INV042()
    {
        FolderWorkspace.From(Root, []).Entries.ShouldBeEmpty();
    }

    [Fact]
    public void From_SkipsNullOrBlankEntries_INV042()
    {
        var workspace = FolderWorkspace.From(Root, [null!, "", "   ", "a.md"]);

        Flatten(workspace).ShouldBe(["File a.md"]);
    }

    [Fact]
    public void From_TreatsABackslashInASegment_AsAFileNameCharacter_INV042()
    {
        // The builder's input contract is '/'-separated; a '\' is an ordinary filename character (the
        // reader normalises real separators to '/'), so this is one File at the root, not "a\b.md".
        var workspace = FolderWorkspace.From(Root, [@"a\b.md"]);

        Flatten(workspace).ShouldBe([@"File a\b.md"]);
    }

    [Fact]
    public void From_GivenNullPaths_Throws_INV042()
    {
        Should.Throw<ArgumentNullException>(() => FolderWorkspace.From(Root, null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void From_GivenNullOrBlankRoot_Throws_INV042(string? root)
    {
        Should.Throw<ArgumentException>(() => FolderWorkspace.From(root!, ["a.md"]));
    }

    [Fact]
    public void Name_IsTheRootFolderName_INV042()
    {
        FolderWorkspace.From(@"C:\vault\Notes", ["a.md"]).Name.ShouldBe("Notes");
    }

    [Fact]
    public void RootPath_IsTheGivenRoot_INV042()
    {
        FolderWorkspace.From(Root, ["a.md"]).RootPath.ShouldBe(Root);
    }

    [Fact]
    public void AbsolutePathOf_ResolvesAFileToItsCanonicalAbsolutePath_INV042()
    {
        var workspace = FolderWorkspace.From(Root, ["sub/note.md"]);
        var file = workspace.Entries[0].Children[0];

        workspace.AbsolutePathOf(file).ShouldBe(Path.GetFullPath(@"C:\vault\sub\note.md"));
    }

    [Fact]
    public void AbsolutePathOf_RoundTripsEveryFilesPath_INV042()
    {
        string[] paths = ["top.md", "a/one.md", "a/b/two.md"];
        var workspace = FolderWorkspace.From(Root, paths);

        void Check(IReadOnlyList<FolderEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.Kind == FolderEntryKind.File)
                {
                    var expected = Path.GetFullPath(Path.Combine(Root, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                    workspace.AbsolutePathOf(entry).ShouldBe(expected);
                }

                Check(entry.Children);
            }
        }

        Check(workspace.Entries);
    }
}
