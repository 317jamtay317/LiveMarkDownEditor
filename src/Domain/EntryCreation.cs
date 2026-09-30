namespace Domain;

/// <summary>
/// The outcome of checking an Entry Name for New File or New Folder (INV-085, INV-086): the tidied Entry
/// Name, and either the File or Folder as it will be once created or the Name Refusal that stops it.
/// Built by <see cref="FolderWorkspace.NewFile"/> and <see cref="FolderWorkspace.NewFolder"/>; it
/// creates nothing itself.
/// </summary>
public sealed record EntryCreation
{
    private EntryCreation(string name, FolderEntry? created, NameRefusal? refusal)
    {
        Name = name;
        Created = created;
        Refusal = refusal;
    }

    /// <summary>
    /// The Entry Name as tidied: each of its names with spaces and trailing dots dropped, joined by
    /// <c>/</c>, and for a new File a Markdown extension kept (e.g. <c>drafts/ideas.md</c> for a typed
    /// <c>drafts/ideas</c>). Empty when the name was blank.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The new, empty entry as it will be once created — the File, or the deepest Folder of the path —
    /// with its relative path from the root. <see langword="null"/> when the Entry Name is refused.
    /// </summary>
    public FolderEntry? Created { get; }

    /// <summary>Why the Entry Name cannot be used, or <see langword="null"/> when it can.</summary>
    public NameRefusal? Refusal { get; }

    /// <summary>A usable Entry Name: <paramref name="created"/> is the entry it makes, named <paramref name="name"/>.</summary>
    internal static EntryCreation To(string name, FolderEntry created) => new(name, created, refusal: null);

    /// <summary>An Entry Name that cannot be used, for the given reason.</summary>
    internal static EntryCreation Refused(string name, NameRefusal refusal) => new(name, created: null, refusal);
}
