namespace Domain;

/// <summary>
/// The outcome of checking a New Name for Rename File (INV-082): the File, its tidied New Name, and
/// either the File as it will be once renamed or the Rename Refusal that stops the rename. Built by
/// <see cref="FolderWorkspace.Rename"/>; it renames nothing itself.
/// </summary>
public sealed record FileRename
{
    private FileRename(FolderEntry file, string newName, FolderEntry? renamed, RenameRefusal? refusal)
    {
        File = file;
        NewName = newName;
        Renamed = renamed;
        Refusal = refusal;
    }

    /// <summary>The File being renamed, as the Folder Tree holds it now.</summary>
    public FolderEntry File { get; }

    /// <summary>
    /// The New Name as tidied: spaces and trailing dots dropped and, once it is usable, a Markdown
    /// extension kept (e.g. <c>Ideas.md</c> for a typed <c>Ideas</c>). Empty when the name was blank.
    /// </summary>
    public string NewName { get; }

    /// <summary>
    /// The File as it will be once renamed: its New Name and its new relative path, in the folder it
    /// already sits in. <see langword="null"/> when the New Name is refused.
    /// </summary>
    public FolderEntry? Renamed { get; }

    /// <summary>Why the New Name cannot be used, or <see langword="null"/> when it can.</summary>
    public RenameRefusal? Refusal { get; }

    /// <summary>
    /// Whether the New Name is exactly the File's current name, so there is nothing to rename. A change
    /// of capitals alone is a change.
    /// </summary>
    public bool IsUnchanged =>
        Renamed is not null && string.Equals(Renamed.Name, File.Name, StringComparison.Ordinal);

    /// <summary>A usable New Name: the File becomes <paramref name="renamed"/>.</summary>
    internal static FileRename To(FolderEntry file, FolderEntry renamed) =>
        new(file, renamed.Name, renamed, refusal: null);

    /// <summary>A New Name that cannot be used, for the given reason.</summary>
    internal static FileRename Refused(FolderEntry file, string newName, RenameRefusal refusal) =>
        new(file, newName, renamed: null, refusal);
}
