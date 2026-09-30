namespace UI.Core;

/// <summary>
/// Tells the user why New File or New Folder created nothing (INV-085, INV-086) — a Name Refusal, or an
/// entry the disk would not create — so the ViewModel can explain it without depending on WPF dialog types
/// (keeping it unit-testable).
/// </summary>
public interface INewEntryNotice
{
    /// <summary>Tells the user why the file or folder was not created.</summary>
    /// <param name="heading">The action that created nothing: "New file" or "New folder".</param>
    /// <param name="reason">Why, as a sentence for the user that names the entry concerned.</param>
    void Explain(string heading, string reason);
}
