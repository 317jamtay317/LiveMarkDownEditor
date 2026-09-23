namespace UI.Core;

/// <summary>
/// Tells the user why Rename File renamed nothing (INV-082) — a Rename Refusal, or a rename the disk
/// would not carry out — so the ViewModel can explain it without depending on WPF dialog types
/// (keeping it unit-testable).
/// </summary>
public interface IRenameFileNotice
{
    /// <summary>Tells the user why the File was not renamed.</summary>
    /// <param name="reason">Why, as a sentence for the user that names the file concerned.</param>
    void Explain(string reason);
}
