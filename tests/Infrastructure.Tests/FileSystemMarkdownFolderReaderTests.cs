using Infrastructure.Storage;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="FileSystemMarkdownFolderReader"/>, the file-system adapter that reads a folder's
/// Folder Listing as root-relative, <c>/</c>-separated paths for the Folder Workspace: every folder, the
/// Markdown files, nothing inside an Excluded Folder (INV-042), and what Git ignores (INV-084). It
/// tolerates a missing root by raising it.
/// </summary>
public sealed class FileSystemMarkdownFolderReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lmde-folder-{Guid.NewGuid():N}");

    private readonly FileSystemMarkdownFolderReader _reader = new(new GitIgnoreChecker());

    private void Write(string relativePath)
    {
        var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "content");
    }

    private void MakeFolder(string relativePath) =>
        Directory.CreateDirectory(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public async Task ReadListingAsync_ListsMarkdownFilesRecursively_AsRelativeSlashPaths_INV042()
    {
        Write("top.md");
        Write("sub/a.md");
        Write("sub/deep/b.markdown");

        var listing = await _reader.ReadListingAsync(_root);

        listing.Files.ShouldBe(["top.md", "sub/a.md", "sub/deep/b.markdown"], ignoreOrder: true);
    }

    [Fact]
    public async Task ReadListingAsync_ListsOnlyMarkdownFiles_INV042()
    {
        Write("keep.md");
        Write("notes.txt");
        Write("sub/image.png");
        Write("sub/data.mdx");

        var listing = await _reader.ReadListingAsync(_root);

        listing.Files.ShouldBe(["keep.md"]);
    }

    [Fact]
    public async Task ReadListingAsync_ListsEveryFolder_EmptyOrNot_INV042()
    {
        Write("notes/a.md");
        Write("assets/logo.png");
        MakeFolder("drafts");
        MakeFolder("a/b/empty");

        var listing = await _reader.ReadListingAsync(_root);

        listing.Folders.ShouldBe(["notes", "assets", "drafts", "a", "a/b", "a/b/empty"], ignoreOrder: true);
    }

    [Fact]
    public async Task ReadListingAsync_ListsAHiddenFolder_AsVsCodeDoes_INV042()
    {
        MakeFolder("secret");
        File.SetAttributes(Path.Combine(_root, "secret"), FileAttributes.Directory | FileAttributes.Hidden);
        Write("secret/note.md");

        var listing = await _reader.ReadListingAsync(_root);

        listing.Folders.ShouldContain("secret");
        listing.Files.ShouldContain("secret/note.md");
    }

    [Fact]
    public async Task ReadListingAsync_ListsFoldersThatOnlyLookLikeNoise_INV042()
    {
        Write("node_modules/pkg/readme.md");
        Write(".obsidian/workspace.md");

        var listing = await _reader.ReadListingAsync(_root);

        listing.Folders.ShouldBe(["node_modules", "node_modules/pkg", ".obsidian"], ignoreOrder: true);
        listing.Files.ShouldBe(["node_modules/pkg/readme.md", ".obsidian/workspace.md"], ignoreOrder: true);
    }

    [Theory]
    [InlineData(".git")]
    [InlineData(".svn")]
    [InlineData(".hg")]
    [InlineData(".jj")]
    public async Task ReadListingAsync_ListsNothingInsideAnExcludedFolder_INV042(string excluded)
    {
        Write("keep.md");
        Write($"{excluded}/config.md");
        Write($"sub/{excluded}/objects/x.md");

        var listing = await _reader.ReadListingAsync(_root);

        listing.Folders.ShouldBe(["sub"]);
        listing.Files.ShouldBe(["keep.md"]);
    }

    [Fact]
    public async Task ReadListingAsync_InAGitRepository_NamesWhatGitIgnores_INV084()
    {
        GitRepository.Init(_root);
        File.WriteAllText(Path.Combine(_root, ".gitignore"), "node_modules/\nscratch.md\n");
        Write("node_modules/pkg/readme.md");
        Write("notes/a.md");
        Write("scratch.md");

        var listing = await _reader.ReadListingAsync(_root);

        listing.Ignored.ShouldContain("node_modules");
        listing.Ignored.ShouldContain("scratch.md");
        listing.Ignored.ShouldNotContain("notes");
        listing.Ignored.ShouldNotContain("notes/a.md");
    }

    [Fact]
    public async Task ReadListingAsync_OutsideAGitRepository_IgnoresNothing_INV084()
    {
        Write("node_modules/pkg/readme.md");

        var listing = await _reader.ReadListingAsync(_root);

        listing.Ignored.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadListingAsync_GivenAMissingRoot_Throws()
    {
        // A missing root is raised, so Restore can distinguish a Folder Workspace that has gone from
        // one that is merely empty (INV-045). DirectoryNotFoundException is an IOException.
        await Should.ThrowAsync<DirectoryNotFoundException>(() => _reader.ReadListingAsync(_root));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReadListingAsync_GivenABlankRoot_Throws(string? root)
    {
        await Should.ThrowAsync<ArgumentException>(() => _reader.ReadListingAsync(root!));
    }

    public void Dispose() => GitRepository.Delete(_root);
}
