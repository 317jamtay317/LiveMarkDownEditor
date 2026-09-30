namespace Domain;

/// <summary>
/// New File and New Folder on a Folder Workspace (INV-085, INV-086): where a new entry goes — the Save
/// Folder — and whether an Entry Name can be used there. It creates nothing itself; the disk is the
/// Infrastructure's.
/// </summary>
public sealed partial class FolderWorkspace
{
    /// <summary>
    /// The Folder a New File or New Folder is created in: the Save Folder, as a Folder of this tree (INV-085,
    /// INV-080). A Selected Folder is that Folder itself; a Selected File is the Folder holding it.
    /// A Selected File at the root, nothing selected, and an entry the live refresh has dropped
    /// (INV-044) all name the root, which is no Folder Entry.
    /// </summary>
    /// <param name="selected">The Selected Folder Entry, or <see langword="null"/> when none is.</param>
    /// <returns>The Folder of this tree to create in, or <see langword="null"/> for the root.</returns>
    public FolderEntry? SaveFolderEntryFor(FolderEntry? selected)
    {
        if (selected is null || !Contains(Entries, selected))
        {
            return null;
        }

        var path = FolderPathOf(selected);
        return path.Length == 0 ? null : FolderAt(Entries, path);
    }

    /// <summary>
    /// Checks an Entry Name for New Folder (INV-085) and says what creating it would make, creating
    /// nothing itself. The Folder goes in the Save Folder for <paramref name="selected"/> (see
    /// <see cref="SaveFolderEntryFor"/>); an Entry Name that is a path creates the Folders on the way too.
    /// Each name is tidied (spaces and trailing dots dropped) and gains no extension. It is refused when a
    /// name is blank, it starts with a separator, a name holds a character Windows forbids or is one
    /// Windows reserves, or the Folder Tree already holds an entry at that path (or a File on the way),
    /// compared without regard to capitals.
    /// </summary>
    /// <param name="selected">The Selected Folder Entry when New Folder began, or <see langword="null"/>.</param>
    /// <param name="entryName">The Entry Name the user typed, or <see langword="null"/>.</param>
    /// <returns>The new, empty Folder, or why it cannot be created, with the tidied Entry Name.</returns>
    public EntryCreation NewFolder(FolderEntry? selected, string? entryName) => NewEntry(selected, entryName, forFile: false);

    /// <summary>
    /// Checks an Entry Name for New File (INV-086) and says what creating it would make, creating nothing
    /// itself. It follows exactly New Folder's rules, except that a last name without a Markdown extension
    /// gains <c>.md</c>, so the new file is a Markdown Document, and an Entry Name ending in a separator
    /// makes a Folder instead, as VS Code's Explorer does.
    /// </summary>
    /// <param name="selected">The Selected Folder Entry when New File began, or <see langword="null"/>.</param>
    /// <param name="entryName">The Entry Name the user typed, or <see langword="null"/>.</param>
    /// <returns>The new, empty File (or Folder), or why it cannot be created, with the tidied Entry Name.</returns>
    public EntryCreation NewFile(FolderEntry? selected, string? entryName) => NewEntry(selected, entryName, forFile: true);

    private EntryCreation NewEntry(FolderEntry? selected, string? entryName, bool forFile)
    {
        if (EntryName.Check(entryName, forFile, out var names, out var isFolder) is { } refusal)
        {
            return EntryCreation.Refused(string.Join('/', names), refusal);
        }

        var name = string.Join('/', names);
        var parent = SaveFolderEntryFor(selected);
        if (IsTaken(parent?.Children ?? Entries, names))
        {
            return EntryCreation.Refused(name, NameRefusal.NameTaken);
        }

        var relativePath = parent is null ? name : $"{parent.RelativePath}/{name}";
        var kind = isFolder ? FolderEntryKind.Folder : FolderEntryKind.File;
        return EntryCreation.To(name, new FolderEntry(kind, names[^1], relativePath, []));
    }

    // Whether the path of names cannot be created beneath the given entries: the last name is already
    // taken by any entry, or a File stands where a Folder on the way should be. A Folder on the way that
    // is not there yet is simply created, so nothing below it can be taken.
    private static bool IsTaken(IReadOnlyList<FolderEntry> entries, string[] names)
    {
        for (var index = 0; index < names.Length; index++)
        {
            var match = entries.FirstOrDefault(entry =>
                string.Equals(entry.Name, names[index], StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return false;
            }

            if (index == names.Length - 1 || match.Kind == FolderEntryKind.File)
            {
                return true;
            }

            entries = match.Children;
        }

        return false;
    }

    // The Folder at the given relative path, or null when the tree holds none there.
    private static FolderEntry? FolderAt(IReadOnlyList<FolderEntry> entries, string folderPath)
    {
        FolderEntry? folder = null;
        foreach (var segment in folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            folder = entries.FirstOrDefault(entry =>
                entry.Kind == FolderEntryKind.Folder && string.Equals(entry.Name, segment, StringComparison.Ordinal));
            if (folder is null)
            {
                return null;
            }

            entries = folder.Children;
        }

        return folder;
    }
}
