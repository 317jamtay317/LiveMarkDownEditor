using System.Windows;
using System.Windows.Documents;

namespace UI.Wysiwyg;

/// <summary>
/// Shared structural walks over a Visual Document, used by the Formatting Actions to find where the
/// caret or selection sits (its enclosing element, or its top-level block).
/// </summary>
internal static class VisualDocumentTraversal
{
    /// <summary>
    /// The nearest ancestor element of type <typeparamref name="T"/> containing
    /// <paramref name="position"/>, or <see langword="null"/> when there is none.
    /// </summary>
    /// <typeparam name="T">The element type to look for (e.g. <see cref="TableCell"/>).</typeparam>
    /// <param name="position">The position whose ancestry is walked.</param>
    internal static T? AncestorOf<T>(TextPointer? position) where T : TextElement
    {
        for (DependencyObject? node = position?.Parent; node is TextElement element; node = element.Parent)
        {
            if (element is T match)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// The block that sits directly in the <see cref="FlowDocument"/> and contains
    /// <paramref name="position"/>, or <see langword="null"/> when the position is not inside one.
    /// </summary>
    /// <param name="position">The position whose top-level block is sought.</param>
    internal static Block? TopLevelBlockOf(TextPointer? position)
    {
        for (DependencyObject? node = position?.Parent; node is TextElement element; node = element.Parent)
        {
            if (element is Block block && block.Parent is FlowDocument)
            {
                return block;
            }
        }

        return null;
    }

    /// <summary>
    /// Guarantees a line below <paramref name="island"/>: appends an empty paragraph when the Block
    /// Island just placed became <paramref name="document"/>'s last block, and returns the paragraph
    /// that now follows it. A Table and a Mermaid Diagram are both blocks the caret cannot type past
    /// from within, so leaving one at the end of the document would strand the user with nowhere to
    /// carry on typing (INV-055).
    /// </summary>
    /// <param name="document">The Visual Document being edited.</param>
    /// <param name="island">The Block Island that must not be the last block.</param>
    /// <returns>The paragraph following <paramref name="island"/>, or <see langword="null"/> when the
    /// block that follows it is not a paragraph (an adjacent Table, say — which is reachable in its
    /// own right, so no new paragraph is minted).</returns>
    internal static Paragraph? EnsureParagraphAfter(FlowDocument document, Block island)
    {
        if (document.Blocks.LastBlock == island)
        {
            var trailing = new Paragraph { Margin = BlockSpacing };
            document.Blocks.Add(trailing);
            return trailing;
        }

        return island.NextBlock as Paragraph;
    }

    // The uniform spacing the Projector gives a body block.
    private static readonly Thickness BlockSpacing = new(0, 0, 0, 6);
}
