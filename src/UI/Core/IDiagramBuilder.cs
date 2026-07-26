namespace UI.Core;

/// <summary>
/// Shows the Diagram Builder — the modal graphical surface that edits the Mermaid Diagram at the
/// caret as a Diagram Graph — and yields the Mermaid source to write back, or <see langword="null"/>
/// when the builder is cancelled (INV-053). Kept behind a port so the editor can open the builder
/// without depending on WPF window types, which is what makes Open Diagram Builder unit-testable —
/// the same reason Insert Link is asked through <see cref="ILinkPrompt"/> (INV-030).
/// </summary>
public interface IDiagramBuilder
{
    /// <summary>
    /// Opens the Diagram Builder, seeded from <paramref name="existingSource"/> — the Mermaid Diagram
    /// at the caret, or <see langword="null"/> to start a new diagram.
    /// </summary>
    /// <param name="existingSource">The Mermaid source to edit graphically, or <see langword="null"/> for a new diagram.</param>
    /// <returns>The Mermaid source to insert, or <see langword="null"/> if the builder was cancelled.</returns>
    string? Build(string? existingSource);
}
