using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// The File this Folder Tree holds for a Watched File, so the Folder Panel can Follow the Active
    /// Session (INV-083). Absolute paths are compared without regard to capitals, exactly as INV-009
    /// compares them, so the same file reached by a different spelling of its path is the same File.
    /// </summary>
    /// <param name="absolutePath">
    /// The absolute path of a Watched File, or <see langword="null"/> when the Active Session has none.
    /// </param>
    /// <returns>
    /// The File of this Folder Tree at that path, or <see langword="null"/> when the path is outside the
    /// root, names a Folder, names no entry the tree holds, or is absent altogether.
    /// </returns>
    public FolderEntry? FileFor(string? absolutePath)
    {
        if (RelativeSegmentsOf(absolutePath) is not { Length: > 0 } segments)
        {
            return null;
        }

        var entries = Entries;
        for (var depth = 0; depth < segments.Length - 1; depth++)
        {
            entries = entries.FirstOrDefault(entry =>
                entry.Kind == FolderEntryKind.Folder && HasName(entry, segments[depth]))?.Children ?? [];
        }

        return entries.FirstOrDefault(entry =>
            entry.Kind == FolderEntryKind.File && HasName(entry, segments[^1]));
    }

    /// <summary>
    /// Whether Delete File may be offered for <paramref name="entry"/> (INV-081): only a File, and only
    /// one this Folder Tree still holds. A Folder is never deletable from the Folder Panel, because it
    /// may hold files the tree does not show (anything that is not Markdown). An entry the live refresh
    /// has dropped (INV-044) is no longer deletable either.
    /// </summary>
    /// <param name="entry">The Folder Entry the user wants to delete, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the entry is a File in this Folder Tree; otherwise <see langword="false"/>.</returns>
    public bool CanDelete([NotNullWhen(true)] FolderEntry? entry) => HoldsFile(entry);

    /// <summary>
    /// Whether Rename File may be offered for <paramref name="entry"/> (INV-082): only a File, and only
    /// one this Folder Tree still holds. A Folder is never renamed from the Folder Panel, and an entry
    /// the live refresh has dropped (INV-044) is no longer renamable either.
    /// </summary>
    /// <param name="entry">The Folder Entry the user wants to rename, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the entry is a File in this Folder Tree; otherwise <see langword="false"/>.</returns>
    public bool CanRename([NotNullWhen(true)] FolderEntry? entry) => HoldsFile(entry);

    /// <summary>
    /// Checks a New Name for Rename File (INV-082) and says what renaming <paramref name="file"/> to it
    /// would do, renaming nothing itself. The name is tidied (spaces and trailing dots dropped) and
    /// keeps the File's own extension when it has no Markdown extension, so the File stays a Markdown
    /// Document in the folder it already sits in. It is refused when it is blank, holds a character
    /// Windows forbids, is a name Windows reserves, or is already another entry's name in the same
    /// folder, compared without regard to capitals. Changing only the capitals of the File's own name
    /// is allowed.
    /// </summary>
    /// <param name="file">The File to rename, which this Folder Tree must hold (see <see cref="CanRename"/>).</param>
    /// <param name="newName">The New Name the user typed, or <see langword="null"/>.</param>
    /// <returns>The File renamed, unchanged, or refused, with the tidied New Name.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="file"/> is not a File this Folder Tree holds.</exception>
    public FileRename Rename(FolderEntry file, string? newName)
    {
        if (!CanRename(file))
        {
            throw new ArgumentException("Only a File this Folder Tree holds can be renamed.", nameof(file));
        }

        if (NewName.Check(newName, Path.GetExtension(file.Name), out var name) is { } refusal)
        {
            return FileRename.Refused(file, name, refusal);
        }

        var folder = FolderPathOf(file);
        var taken = EntriesIn(folder).Any(sibling =>
            !IsSameEntry(sibling, file) && string.Equals(sibling.Name, name, StringComparison.OrdinalIgnoreCase));
        if (taken)
        {
            return FileRename.Refused(file, name, RenameRefusal.NameTaken);
        }

        var relativePath = folder.Length == 0 ? name : $"{folder}/{name}";
        return FileRename.To(file, new FolderEntry(FolderEntryKind.File, name, relativePath, []));
    }

    // The path's segments beneath the root — the way down the Folder Tree to the entry it names — or
    // null when it is not beneath the root at all, or is the root itself (which names no entry).
    private string[]? RelativeSegmentsOf(string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return null;
        }

        string path;
        string root;
        try
        {
            path = Path.GetFullPath(absolutePath);
            root = Path.GetFullPath(RootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A path Windows itself cannot make sense of names no File.
            return null;
        }

        if (path.Length <= root.Length + 1
            || !path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || !IsSeparator(path[root.Length]))
        {
            return null;
        }

        return path[(root.Length + 1)..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool IsSeparator(char character) =>
        character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar;

    // A Windows file name is case-insensitive, so the tree's own name for an entry and the Watched
    // File's spelling of it are the same name.
    private static bool HasName(FolderEntry entry, string name) =>
        string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase);

    // A File this Folder Tree still holds: the entries Delete File and Rename File may act on.
    private bool HoldsFile([NotNullWhen(true)] FolderEntry? entry) =>
        entry is { Kind: FolderEntryKind.File } && Contains(Entries, entry);

    // The entries directly inside the folder at the given relative path; the root is the empty path.
    private IReadOnlyList<FolderEntry> EntriesIn(string folderPath)
    {
        var entries = Entries;
        foreach (var segment in folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            entries = entries.FirstOrDefault(entry =>
                          entry.Kind == FolderEntryKind.Folder
                          && string.Equals(entry.Name, segment, StringComparison.Ordinal))?.Children
                      ?? [];
        }

        return entries;
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
        entries.Any(entry => IsSameEntry(entry, sought) || Contains(entry.Children, sought));

    private static bool IsSameEntry(FolderEntry entry, FolderEntry sought) =>
        entry.Kind == sought.Kind
        && string.Equals(entry.RelativePath, sought.RelativePath, StringComparison.Ordinal);

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
