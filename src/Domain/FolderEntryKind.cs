namespace Domain;

/// <summary>
/// Which of the two kinds a <see cref="FolderEntry"/> is: a <see cref="Folder"/> (a branch that holds
/// child Folder Entries) or a <see cref="File"/> (a leaf naming a Markdown Document). This is a Folder
/// Entry's <c>Folder Entry Kind</c> (INV-042).
/// </summary>
public enum FolderEntryKind
{
    /// <summary>A branch of the Folder Tree — a directory holding child Folder Entries.</summary>
    Folder,

    /// <summary>A leaf of the Folder Tree — a Markdown Document that opens as a Watched File when activated.</summary>
    File,
}
