namespace UI.Core;

/// <summary>
/// Supplies the bundled Mermaid library text embedded into a Standalone Page so its Mermaid Diagrams
/// render in a browser (INV-049). An adapter reads it from the app's bundled assets; when it cannot
/// be read it yields <see langword="null"/>, and the export carries the diagrams as unrendered code
/// blocks (Render stays pure — INV-002).
/// </summary>
public interface IMermaidScriptSource
{
    /// <summary>The Mermaid library script text, or <see langword="null"/> when it is unavailable.</summary>
    /// <returns>The script text to inline, or <see langword="null"/>.</returns>
    string? Read();
}
