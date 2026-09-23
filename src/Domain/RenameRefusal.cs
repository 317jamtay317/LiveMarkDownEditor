namespace Domain;

/// <summary>
/// A Rename Refusal: why a New Name cannot be used, so Rename File changes nothing and tells the user
/// (INV-082).
/// </summary>
public enum RenameRefusal
{
    /// <summary>The New Name is empty once tidied: nothing but spaces and dots.</summary>
    Blank,

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
