namespace Domain;

/// <summary>
/// A Name Refusal: why a New Name or an Entry Name cannot be used, so Rename File, New File or New Folder changes
/// nothing and tells the user (INV-082, INV-085, INV-086). Where a member speaks of the New Name, an Entry Name is
/// refused for exactly the same reason.
/// </summary>
public enum NameRefusal
{
    /// <summary>The New Name is empty once tidied: nothing but spaces and dots.</summary>
    Blank,

    /// <summary>
    /// The Entry Name starts with a separator (<c>/</c> or <c>\</c>), which would reach outside the
    /// Folder Workspace rather than into the Save Folder (INV-085, INV-086).
    /// </summary>
    StartsWithSeparator,

    /// <summary>
    /// The New Name holds a character Windows forbids in a file name (<c>\ / : * ? " &lt; &gt; |</c>, or
    /// a control character). Refusing a path separator is also what keeps the File in its own folder.
    /// </summary>
    InvalidCharacter,

    /// <summary>
    /// The New Name is one Windows reserves for a device (<c>CON</c>, <c>PRN</c>, <c>AUX</c>, <c>NUL</c>,
    /// <c>COM1</c>–<c>COM9</c>, <c>LPT1</c>–<c>LPT9</c>), with or without an extension.
    /// </summary>
    ReservedName,

    /// <summary>
    /// Another entry in the same folder already has the New Name, compared without regard to capitals,
    /// so renaming would overwrite it.
    /// </summary>
    NameTaken,
}
