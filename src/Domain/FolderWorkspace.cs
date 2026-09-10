using System.IO;

namespace Domain;

/// <summary>
/// A Folder Workspace: a root folder opened to browse its Markdown Documents as a Folder Tree, turning
/// the editor into a lightweight knowledge base. It is a pure, deterministic projection of the root and
/// the set of file paths beneath it — only Markdown files appear, Markdown-empty folders are pruned, and
/// folders sort before files (each A–Z, case-insensitively) — so the same inputs always yield the same
/// tree (INV-042). It is distinct from the <c>Workspace</c> (the open Editor Sessions shown as Tabs):
/// a Folder Workspace is a folder on disk being browsed.
/// </summary>
public sealed class FolderWorkspace
{
    private FolderWorkspace(string rootPath, string name, IReadOnlyList<FolderEntry> entries)
    {
        RootPath = rootPath;
        Name = name;
        Entries = entries;
    }

    /// <summary>The absolute path of the opened root folder.</summary>
    public string RootPath { get; }

    /// <summary>The root folder's display name — the last segment of <see cref="RootPath"/>.</summary>
    public string Name { get; }

    /// <summary>The top-level Folder Entries of the Folder Tree, folders before files, each A–Z.</summary>
    public IReadOnlyList<FolderEntry> Entries { get; }

    /// <summary>
    /// Builds a Folder Workspace from the root path and the root-relative, <c>/</c>-separated paths of
    /// the files beneath it. Non-Markdown files are dropped (INV-042), so any folder they alone would
    /// have created never appears — pruning is inherent. The result is deterministic regardless of the
    /// order the paths arrive in.
    /// </summary>
    /// <param name="rootPath">The absolute path of the opened root folder.</param>
    /// <param name="relativeMarkdownPaths">
    /// The files beneath the root, each a root-relative <c>/</c>-separated path. Null or blank entries
    /// are skipped; non-Markdown entries are ignored.
    /// </param>
    /// <returns>The Folder Workspace presenting the pruned, ordered Folder Tree.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rootPath"/> is null or blank.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="relativeMarkdownPaths"/> is null.</exception>
    public static FolderWorkspace From(string rootPath, IEnumerable<string> relativeMarkdownPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(relativeMarkdownPaths);

        var root = new Builder();
        foreach (var path in relativeMarkdownPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            // The input contract is '/'-separated; only real path separators split segments.
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || !MarkdownFile.IsMarkdown(segments[^1]))
            {
                continue;
            }

            var folder = root;
            for (var depth = 0; depth < segments.Length - 1; depth++)
            {
                folder = folder.Folder(segments[depth]);
            }

            folder.AddFile(segments[^1]);
        }

        return new FolderWorkspace(rootPath, DisplayName(rootPath), root.ToEntries());
    }

    /// <summary>
    /// Resolves a Folder Entry to its canonical absolute path — <see cref="RootPath"/> combined with the
    /// entry's relative path — so a file opened from the Folder Tree is the same path string as the same
    /// file opened through the picker, which is what lets INV-009 dedupe them.
    /// </summary>
    /// <param name="entry">The Folder Entry to resolve (typically a File).</param>
    /// <returns>The entry's canonical absolute path.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is null.</exception>
    public string AbsolutePathOf(FolderEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var relative = entry.RelativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(RootPath, relative));
    }

    /// <summary>
    /// The Save Folder for a new Markdown Document: the folder the Folder Workspace offers to save
    /// into, given the Selected Folder Entry (INV-080). A Selected Folder is that folder itself; a
    /// Selected File is the folder holding it, so a new document lands beside the one being read
    /// rather than inside it. Nothing selected — and an entry that is no longer in the Folder Tree,
    /// which the live refresh can leave behind (INV-044) — both name the root.
    /// </summary>
    /// <param name="selected">The Selected Folder Entry, or <see langword="null"/> when none is.</param>
    /// <returns>The canonical absolute path of the folder to save into.</returns>
    public string SaveFolderFor(FolderEntry? selected)
    {
        var relative = selected is not null && Contains(Entries, selected)
            ? FolderPathOf(selected)
            : string.Empty;

        return Path.GetFullPath(Path.Combine(RootPath, relative.Replace('/', Path.DirectorySeparatorChar)));
    }

    // A Folder names itself; a File names the folder it sits in — everything before its last '/', or
    // the root when it has none.
    private static string FolderPathOf(FolderEntry entry)
    {
        if (entry.Kind == FolderEntryKind.Folder)
        {
            return entry.RelativePath;
        }

        var slash = entry.RelativePath.LastIndexOf('/');
        return slash < 0 ? string.Empty : entry.RelativePath[..slash];
    }

    // Whether this Folder Tree still holds the given entry. The tree is rebuilt on every refresh, so a
    // remembered entry is matched by what identifies it — its kind and its relative path — rather than
    // by reference.
    private static bool Contains(IReadOnlyList<FolderEntry> entries, FolderEntry sought) =>
        entries.Any(entry =>
            (entry.Kind == sought.Kind
             && string.Equals(entry.RelativePath, sought.RelativePath, StringComparison.Ordinal))
            || Contains(entry.Children, sought));

    private static string DisplayName(string rootPath)
    {
        var trimmed = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    /// <summary>
    /// Mutable scaffolding for one folder while the tree is assembled. Folders and files are kept in
    /// separate maps (keyed by segment name) so a folder and a file that share a name coexist, and so
    /// the folders-before-files ordering falls out naturally. A Builder is created only while walking
    /// toward a Markdown file, so every Builder yields at least one File — hence no empty branches.
    /// </summary>
    private sealed class Builder
    {
        private readonly Dictionary<string, Builder> _folders = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        private Builder(string relativePath) => RelativePath = relativePath;

        public Builder() => RelativePath = string.Empty;

        private string RelativePath { get; }

        public Builder Folder(string name)
        {
            if (!_folders.TryGetValue(name, out var child))
            {
                child = new Builder(Join(RelativePath, name));
                _folders.Add(name, child);
            }

            return child;
        }

        public void AddFile(string name) => _files[name] = Join(RelativePath, name);

        public IReadOnlyList<FolderEntry> ToEntries()
        {
            var entries = new List<FolderEntry>(_folders.Count + _files.Count);

            foreach (var folder in _folders.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(new FolderEntry(
                    FolderEntryKind.Folder, folder.Key, folder.Value.RelativePath, folder.Value.ToEntries()));
            }

            foreach (var file in _files.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(new FolderEntry(FolderEntryKind.File, file.Key, file.Value, []));
            }

            return entries;
        }

        private static string Join(string parent, string name) =>
            parent.Length == 0 ? name : $"{parent}/{name}";
    }
}
