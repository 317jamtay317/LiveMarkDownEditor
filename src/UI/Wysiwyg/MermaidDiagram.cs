using System.Windows;
using System.Windows.Documents;
using UI.Controls;

namespace UI.Wysiwyg;

/// <summary>
/// The Mermaid Diagram seam: identifies a Mermaid Diagram by its language, builds the atomic block that
/// shows its rendered picture in the Visual Document, and reads a diagram block's source back so the
/// Diagram Preview and the Flowchart Builder can use it. Pure and view-only — it never changes the
/// Markdown Document (INV-047).
/// </summary>
public static class MermaidDiagram
{
    /// <summary>The Code Block language (info string) that marks a Mermaid Diagram.</summary>
    public const string Language = "mermaid";

    /// <summary>
    /// Whether <paramref name="language"/> names a Mermaid Diagram — the info string <c>mermaid</c>,
    /// compared case-insensitively and ignoring surrounding whitespace.
    /// </summary>
    /// <param name="language">The Code Block's language (info string), or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it names a Mermaid Diagram; otherwise <see langword="false"/>.</returns>
    public static bool IsMermaidLanguage(string? language) =>
        string.Equals(language?.Trim(), Language, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The source of the Mermaid Diagram <paramref name="block"/> is, or <see langword="null"/> when the
    /// block is not a Mermaid Diagram. Reading it is view-only (INV-047).
    /// </summary>
    /// <param name="block">The Visual Document block to inspect, or <see langword="null"/>.</param>
    /// <returns>The diagram's source, or <see langword="null"/>.</returns>
    public static string? SourceOfBlock(Block? block) =>
        block is BlockUIContainer { Tag: MermaidDiagramRole role } ? role.Source : null;

    /// <summary>
    /// The source of the Mermaid Diagram the caret at <paramref name="caret"/> is within, or
    /// <see langword="null"/> when the caret is not on a Mermaid Diagram. Reading it is view-only
    /// (INV-047).
    /// </summary>
    /// <param name="caret">The caret position to inspect, or <see langword="null"/>.</param>
    /// <returns>The diagram's source, or <see langword="null"/>.</returns>
    public static string? SourceAt(TextPointer? caret) =>
        caret is null ? null : SourceOfBlock(VisualDocumentTraversal.TopLevelBlockOf(caret));

    /// <summary>
    /// Builds the atomic block a Mermaid Diagram is shown as: a <see cref="BlockUIContainer"/> hosting a
    /// <see cref="MermaidDiagramView"/>, tagged with a <see cref="MermaidDiagramRole"/> carrying the
    /// source so Capture re-emits the fenced ```mermaid``` block (INV-047). The picture itself is
    /// rendered afterwards by the editor's coordinator, so this stays a pure structural projection
    /// (INV-003).
    /// </summary>
    /// <param name="source">The Mermaid Diagram source.</param>
    /// <returns>The diagram block.</returns>
    public static BlockUIContainer CreateDiagramBlock(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new BlockUIContainer(new MermaidDiagramView { Source = source })
        {
            Tag = new MermaidDiagramRole(source),
            Margin = new Thickness(0, 0, 0, 6),
        };
    }
}
