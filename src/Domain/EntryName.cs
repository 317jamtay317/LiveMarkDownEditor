namespace Domain;

/// <summary>
/// The rules an Entry Name must meet for New File and New Folder, apart from whether the Folder Tree
/// already holds it (INV-085, INV-086). As in VS Code's Explorer, an Entry Name may be a path of names
/// separated by <c>/</c> or <c>\</c>, whose Folders on the way are created too; each name is tidied and
/// checked as a New Name is. For New File, a last name without a Markdown extension gains <c>.md</c>, and
/// an Entry Name ending in a separator is a Folder.
/// </summary>
internal static class EntryName
{
    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>Tidies <paramref name="typed"/> into the names of an Entry Name and checks them.</summary>
    /// <param name="typed">The Entry Name the user typed, or <see langword="null"/>.</param>
    /// <param name="forFile">Whether it names a new File (New File) rather than a Folder (New Folder).</param>
    /// <param name="names">The tidied names, from the Save Folder down to the new entry.</param>
    /// <param name="isFolder">Whether the entry is a Folder: always for New Folder, and for New File when the name ends in a separator.</param>
    /// <returns>Why the Entry Name cannot be used, or <see langword="null"/> when it can.</returns>
    public static NameRefusal? Check(string? typed, bool forFile, out string[] names, out bool isFolder)
    {
        var text = (typed ?? string.Empty).Trim();
        names = [];
        isFolder = !forFile || (text.Length > 0 && Separators.Contains(text[^1]));
        if (text.Length == 0)
        {
            return NameRefusal.Blank;
        }

        if (Separators.Contains(text[0]))
        {
            return NameRefusal.StartsWithSeparator;
        }

        // Every name in the path, tidied; a separator left over at the end marks a Folder, not a name.
        names = text.TrimEnd(Separators).Split(Separators).Select(NewName.Tidy).ToArray();
        foreach (var name in names)
        {
            if ((NewName.Unusable(name) ?? NewName.ReservedOrNull(name)) is { } refusal)
            {
                return refusal;
            }
        }

        if (!isFolder && !MarkdownFile.IsMarkdown(names[^1]))
        {
            names[^1] += ".md";
            return NewName.ReservedOrNull(names[^1]);
        }

        return null;
    }
}
