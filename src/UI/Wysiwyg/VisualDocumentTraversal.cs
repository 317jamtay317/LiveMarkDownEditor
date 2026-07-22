using System.Text;
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
    /// <remarks>
    /// A position at the document's own edge — which is where Select All leaves a selection's end —
    /// has the <see cref="FlowDocument"/> itself as its parent, so it is inside no block at all. It
    /// still <em>borders</em> one, and that is the block the selection reaches: the block-spanning
    /// actions (Toggle Code, Toggle Block Quote, Toggle List) would otherwise read a Select All as
    /// "ends nowhere" and decline to run, or run on the first block alone.
    /// </remarks>
    /// <param name="position">The position whose top-level block is sought.</param>
    /// <param name="direction">Which neighbour to take when the position lies between blocks rather
    /// than inside one: <see cref="LogicalDirection.Forward"/> for the block a selection starts in,
    /// <see cref="LogicalDirection.Backward"/> for the one it ends in.</param>
    internal static Block? TopLevelBlockOf(
        TextPointer? position,
        LogicalDirection direction = LogicalDirection.Forward)
    {
        for (DependencyObject? node = position?.Parent; node is TextElement element; node = element.Parent)
        {
            if (element is Block block && block.Parent is FlowDocument)
            {
                return block;
            }
        }

        return position?.Parent is FlowDocument ? position.GetAdjacentElement(direction) as Block : null;
    }

    /// <summary>
    /// The text <paramref name="range"/> actually holds, read from its own runs.
    /// </summary>
    /// <remarks>
    /// <see cref="TextRange.Text"/> cannot be used where the text is about to be written back: a range
    /// that starts at a List Item's first insertion position reads that item's bullet back as a literal
    /// <c>"•\t"</c>, so re-inserting it would type the marker into the document as content. Reading the
    /// runs takes only what the user really selected.
    /// </remarks>
    /// <param name="range">The range whose text is read.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="range"/> is <see langword="null"/>.</exception>
    internal static string TextIn(TextRange range)
    {
        ArgumentNullException.ThrowIfNull(range);

        var builder = new StringBuilder();
        for (var pointer = range.Start;
             pointer is not null && pointer.CompareTo(range.End) < 0;
             pointer = pointer.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (pointer.GetAdjacentElement(LogicalDirection.Forward) is LineBreak)
            {
                // A line break is not text but it is content: dropping it would silently join the two
                // lines it separates.
                builder.Append('\n');
                continue;
            }

            if (pointer.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text)
            {
                continue;
            }

            // Within a run one symbol is one character, so the offset to the range's end clamps a run
            // that carries on past it.
            var text = pointer.GetTextInRun(LogicalDirection.Forward);
            var overshoot = pointer.GetOffsetToPosition(range.End);
            builder.Append(overshoot < text.Length ? text[..overshoot] : text);
        }

        return builder.ToString();
    }

    /// <summary>
    /// <paramref name="range"/> narrowed to its non-whitespace core, or an empty range at its start
    /// when it holds nothing but whitespace.
    /// </summary>
    /// <remarks>
    /// Double-clicking a word selects its trailing space too, so a Formatting Action that rebuilds the
    /// selected text — Toggle Code, Insert Link, Insert Image — would otherwise pull that space inside
    /// its delimiters (`` `fast `now ``, `[docs ](url)here`), shading or linking the separator and
    /// leaving the next word butted against it. Narrowing first keeps the space where the user put it:
    /// in the sentence, outside the markup (INV-018).
    /// </remarks>
    /// <param name="range">The range to narrow.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="range"/> is <see langword="null"/>.</exception>
    internal static TextRange WithoutSurroundingWhitespace(TextRange range)
    {
        ArgumentNullException.ThrowIfNull(range);

        var start = SkipWhitespace(range.Start, range.End, LogicalDirection.Forward);
        if (start.CompareTo(range.End) >= 0)
        {
            return new TextRange(range.Start, range.Start);
        }

        return new TextRange(start, SkipWhitespace(range.End, start, LogicalDirection.Backward));
    }

    // Walks one symbol at a time from `from` towards `limit`, stopping at the first text character
    // that is not whitespace. Element edges (which carry no text) are stepped over, so a selection
    // that starts or ends against a run boundary narrows as readily as one inside a single run — and
    // the walk ends up *inside* a run rather than against its edge, which matters: a range that
    // begins at a List Item's first paragraph reads its bullet back as literal "•\t" text.
    // The characters are read through GetTextInRun, which returns only what a run really holds.
    private static TextPointer SkipWhitespace(TextPointer from, TextPointer limit, LogicalDirection direction)
    {
        var forward = direction == LogicalDirection.Forward;
        var pointer = from;
        while (forward ? pointer.CompareTo(limit) < 0 : pointer.CompareTo(limit) > 0)
        {
            if (pointer.GetPointerContext(direction) == TextPointerContext.Text)
            {
                var text = pointer.GetTextInRun(direction);
                if (text.Length > 0 && !char.IsWhiteSpace(forward ? text[0] : text[^1]))
                {
                    break;
                }
            }

            var next = pointer.GetPositionAtOffset(forward ? 1 : -1, direction);
            if (next is null)
            {
                break;
            }

            pointer = next;
        }

        return pointer;
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
