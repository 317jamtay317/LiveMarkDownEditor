using Application;
using Domain;

namespace Infrastructure.Storage;

/// <summary>
/// File-system adapter for <see cref="IMarkdownFolderReader"/>. Enumerates the Markdown Documents
/// beneath a root folder as root-relative, <c>/</c>-separated paths for <c>FolderWorkspace.From</c>
/// (INV-042). Symlink/junction loops, hidden and system entries, and inaccessible locations are skipped
/// by the enumeration itself; a small set of noise directories (<c>.git</c>, <c>.obsidian</c>,
/// <c>node_modules</c>) is excluded by name so a scanned repository or vault does not drown the tree.
/// </summary>
public sealed class FileSystemMarkdownFolderReader : IMarkdownFolderReader
{
    private static readonly HashSet<string> NoiseDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".obsidian",
        "node_modules",
    };

    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System,
    };

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> EnumerateMarkdownFilesAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        return Task.Run(() => Enumerate(rootPath, cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<string> Enumerate(string rootPath, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root))
        {
            // A root that is not there is raised (not silently empty), so Restore can tell a Folder
            // Workspace that has gone from one that is merely empty (INV-045).
            throw new DirectoryNotFoundException($"Folder not found: {root}");
        }

        var results = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", Options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!MarkdownFile.IsMarkdown(file))
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (!IsInNoiseDirectory(relative))
            {
                results.Add(relative);
            }
        }

        return results;
    }

    private static bool IsInNoiseDirectory(string relativePath)
    {
        var segments = relativePath.Split('/');

        // Only the directory segments matter; the final segment is the file itself.
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (NoiseDirectories.Contains(segments[index]))
            {
                return true;
            }
        }

        return false;
    }
}
