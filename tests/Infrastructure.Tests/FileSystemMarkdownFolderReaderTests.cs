using Infrastructure.Storage;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="FileSystemMarkdownFolderReader"/>, the file-system adapter that enumerates a
/// folder's Markdown Documents as root-relative, <c>/</c>-separated paths for the Folder Workspace
/// (INV-042). It keeps only Markdown files, skips noise directories, and tolerates a missing root.
/// </summary>
public sealed class FileSystemMarkdownFolderReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lmde-folder-{Guid.NewGuid():N}");

    private void Write(string relativePath)
    {
        var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "content");
    }

    [Fact]
    public async Task EnumerateMarkdownFilesAsync_ReturnsMarkdownFilesRecursively_AsRelativeSlashPaths()
    {
        Write("top.md");
        Write("sub/a.md");
        Write("sub/deep/b.markdown");

        var files = await new FileSystemMarkdownFolderReader().EnumerateMarkdownFilesAsync(_root);

        files.ShouldBe(["top.md", "sub/a.md", "sub/deep/b.markdown"], ignoreOrder: true);
    }

    [Fact]
    public async Task EnumerateMarkdownFilesAsync_ExcludesNonMarkdownFiles()
    {
        Write("keep.md");
        Write("notes.txt");
        Write("sub/image.png");
        Write("sub/data.mdx");

        var files = await new FileSystemMarkdownFolderReader().EnumerateMarkdownFilesAsync(_root);

        files.ShouldBe(["keep.md"]);
    }

    [Fact]
    public async Task EnumerateMarkdownFilesAsync_ExcludesNoiseDirectories()
    {
        Write("keep.md");
        Write(".git/config.md");
        Write("node_modules/pkg/readme.md");
        Write(".obsidian/workspace.md");

        var files = await new FileSystemMarkdownFolderReader().EnumerateMarkdownFilesAsync(_root);

        files.ShouldBe(["keep.md"]);
    }

    [Fact]
    public async Task EnumerateMarkdownFilesAsync_GivenAMissingRoot_Throws()
    {
        // A missing root is raised, so Restore can distinguish a Folder Workspace that has gone from
        // one that is merely empty (INV-045). DirectoryNotFoundException is an IOException.
        var reader = new FileSystemMarkdownFolderReader();

        await Should.ThrowAsync<DirectoryNotFoundException>(() => reader.EnumerateMarkdownFilesAsync(_root));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EnumerateMarkdownFilesAsync_GivenABlankRoot_Throws(string? root)
    {
        var reader = new FileSystemMarkdownFolderReader();

        await Should.ThrowAsync<ArgumentException>(() => reader.EnumerateMarkdownFilesAsync(root!));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
