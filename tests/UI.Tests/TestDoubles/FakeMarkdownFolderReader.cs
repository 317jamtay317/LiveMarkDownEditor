using System.IO;
using Application;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Test double for <see cref="IMarkdownFolderReader"/>. Returns a scriptable set of relative Markdown
/// paths for any readable root, records the last root it was asked for, and can be told which roots are
/// "gone" so it throws <see cref="DirectoryNotFoundException"/> for them (as the real reader does).
/// </summary>
public sealed class FakeMarkdownFolderReader : IMarkdownFolderReader
{
    /// <summary>The relative Markdown paths returned for a readable root. Mutate between calls to simulate the folder changing.</summary>
    public IReadOnlyList<string> Result { get; set; } = [];

    /// <summary>Roots the reader treats as gone — enumerating one throws, like a folder that no longer exists.</summary>
    public HashSet<string> MissingRoots { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The root most recently passed to <see cref="EnumerateMarkdownFilesAsync"/>.</summary>
    public string? LastRoot { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> EnumerateMarkdownFilesAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        LastRoot = rootPath;
        return MissingRoots.Contains(rootPath)
            ? Task.FromException<IReadOnlyList<string>>(new DirectoryNotFoundException($"Folder not found: {rootPath}"))
            : Task.FromResult(Result);
    }
}
