namespace UI.Core;

/// <summary>
/// Asks the user whether they are sure they want to Delete File (INV-081), so the ViewModel can gate
/// the delete on the answer without depending on WPF dialog types (keeping it unit-testable).
/// </summary>
public interface IDeleteFilePrompt
{
    /// <summary>Asks whether the user is sure they want to delete the named File.</summary>
    /// <param name="fileName">The display name of the File to delete (e.g. <c>note.md</c>).</param>
    /// <returns><see langword="true"/> when the user confirms; <see langword="false"/> to keep the File.</returns>
    bool Confirm(string fileName);
}
