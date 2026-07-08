namespace UI.Core;

/// <summary>The user's decision when asked to close an Editor Session that still has unsaved edits.</summary>
public enum UnsavedEditsDecision
{
    /// <summary>Persist the unsaved edits, then close the Editor Session.</summary>
    Save,

    /// <summary>Close the Editor Session without saving, discarding the unsaved edits.</summary>
    Discard,

    /// <summary>Abort the close and keep the Editor Session open.</summary>
    Cancel,
}

/// <summary>
/// Asks the user how to resolve closing an Editor Session that holds unsaved edits, so the close
/// decision (INV-010) can be made without the ViewModel depending on WPF dialog types (keeping it
/// unit-testable).
/// </summary>
public interface IUnsavedEditsPrompt
{
    /// <summary>Asks whether to save, discard, or cancel closing a document with unsaved edits.</summary>
    /// <param name="documentName">The display name of the document being closed.</param>
    /// <returns>The user's <see cref="UnsavedEditsDecision"/>.</returns>
    UnsavedEditsDecision Confirm(string documentName);
}
