namespace Domain;

/// <summary>
/// One node of a Folder Tree (INV-042): a <see cref="FolderEntryKind.Folder"/> holding child Folder
/// Entries, or a <see cref="FolderEntryKind.File"/> naming a Markdown Document. A File has no children.
/// It is presentation-free — it carries no fold, selection, or command state (that lives in the UI,
/// the reason the Outline's <c>OutlineEntry</c> lives in UI.Controls rather than the Domain) — so the
/// Folder Panel can bind straight to it.
/// </summary>
public sealed record FolderEntry
{
    /// <summary>Creates a Folder Entry.</summary>
    /// <param name="kind">Whether this entry is a Folder (a branch) or a File (a leaf).</param>
    /// <param name="name">The entry's display name — the last path segment (e.g. <c>note.md</c>).</param>
    /// <param name="relativePath">The entry's path relative to the Folder Workspace root, <c>/</c>-separated.</param>
    /// <param name="children">The child Folder Entries (empty for a File), folders before files, each A–Z.</param>
    /// <exception cref="ArgumentNullException">Thrown when any reference argument is <see langword="null"/>.</exception>
    public FolderEntry(FolderEntryKind kind, string name, string relativePath, IReadOnlyList<FolderEntry> children)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(children);

        Kind = kind;
        Name = name;
        RelativePath = relativePath;
        Children = children;
    }

    /// <summary>Whether this entry is a Folder (a branch) or a File (a leaf).</summary>
    public FolderEntryKind Kind { get; }

    /// <summary>The entry's display name — the last segment of its <see cref="RelativePath"/>.</summary>
    public string Name { get; }

    /// <summary>The entry's path relative to the Folder Workspace root, <c>/</c>-separated.</summary>
    public string RelativePath { get; }

    /// <summary>The child Folder Entries — folders before files, each ordered A–Z. Empty for a File.</summary>
    public IReadOnlyList<FolderEntry> Children { get; }
}
