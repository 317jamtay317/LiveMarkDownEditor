using System.IO;
using Application;
using Domain;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Test double for <see cref="IMarkdownFolderReader"/>. Returns a scriptable Folder Listing for any
/// readable root, records the last root it was asked for, and can be told which roots are "gone" so it
/// throws <see cref="DirectoryNotFoundException"/> for them (as the real reader does).
/// </summary>
public sealed class FakeMarkdownFolderReader : IMarkdownFolderReader
{
    /// <summary>The relative file paths listed for a readable root. Mutate between calls to simulate the folder changing.</summary>
    public IReadOnlyList<string> Result { get; set; } = [];

    /// <summary>The relative folder paths listed for a readable root, beyond those the files sit in (INV-042).</summary>
    public IReadOnlyList<string> Folders { get; set; } = [];

    /// <summary>The listed paths Git ignores (INV-084).</summary>
    public IReadOnlyList<string> Ignored { get; set; } = [];

    /// <summary>Roots the reader treats as gone — reading one throws, like a folder that no longer exists.</summary>
    public HashSet<string> MissingRoots { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The root most recently passed to <see cref="ReadListingAsync"/>.</summary>
    public string? LastRoot { get; private set; }

    /// <inheritdoc />
    public Task<FolderListing> ReadListingAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        LastRoot = rootPath;
        return MissingRoots.Contains(rootPath)
            ? Task.FromException<FolderListing>(new DirectoryNotFoundException($"Folder not found: {rootPath}"))
            : Task.FromResult(new FolderListing(Folders, Result, Ignored));
    }
}
