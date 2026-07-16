using System.Text;
using System.Windows.Documents;

namespace UI.Find;

/// <summary>
/// Maps the Matches of a query onto a Visual Document: it builds a plain-text snapshot of the
/// document, delegates the search to the pure <see cref="MatchFinder"/>, and resolves each Match's
/// offsets back into a document range. It only ever reads the document, so finding never changes the
/// Markdown Document (INV-016) — it is the Find counterpart of
/// <see cref="Wysiwyg.CodeShadingScanner"/>.
/// </summary>
public static class MatchScanner
{
    /// <summary>
    /// Finds every occurrence of <paramref name="query"/> in <paramref name="document"/> and returns
    /// one range per Match. A Match may span an inline formatting boundary — it still yields a single
    /// contiguous range — but never bridges two blocks.
    /// </summary>
    /// <param name="document">The Visual Document to search; only read, never changed.</param>
    /// <param name="query">The query to find; an empty or null query yields no Matches.</param>
    /// <returns>The Matches as ranges in document order; empty when there are none.</returns>
    public static IReadOnlyList<TextRange> Scan(FlowDocument? document, string? query)
    {
        var ranges = new List<TextRange>();
        if (document is null || string.IsNullOrEmpty(query))
        {
            return ranges;
        }

        var (text, anchors) = BuildTextSnapshot(document);
        foreach (var match in MatchFinder.FindMatches(text, query))
        {
            if (RangeFor(anchors, match) is { } range)
            {
                ranges.Add(range);
            }
        }

        return ranges;
    }

    // Maps a Match (offsets into the text snapshot) to a document range, resolving its start and end
    // through their anchoring text runs — so a Match that spans an inline formatting boundary still
    // yields one contiguous range.
    private static TextRange? RangeFor(IReadOnlyList<(int Offset, int Length, TextPointer Pointer)> anchors, Match match)
    {
        var start = PointerAt(anchors, match.Start, atEnd: false);
        var end = PointerAt(anchors, match.Start + match.Length, atEnd: true);
        return start is null || end is null ? null : new TextRange(start, end);
    }

    private static TextPointer? PointerAt(
        IReadOnlyList<(int Offset, int Length, TextPointer Pointer)> anchors,
        int index,
        bool atEnd)
    {
        foreach (var anchor in anchors)
        {
            // For a span's end, the character before the index must lie in this run (offset < index);
            // for a start, the character at the index must (offset <= index).
            var withinStart = atEnd ? index > anchor.Offset : index >= anchor.Offset;
            var withinEnd = index <= anchor.Offset + anchor.Length;
            if (withinStart && withinEnd)
            {
                return anchor.Pointer.GetPositionAtOffset(index - anchor.Offset, LogicalDirection.Forward);
            }
        }

        return null;
    }

    // A plain-text snapshot of the Visual Document for searching, with an anchor per text run recording
    // where that run's text begins in the snapshot. A separator is inserted at block boundaries so a
    // Match never bridges two blocks, while adjacent inline runs are concatenated so a Match may span a
    // formatting boundary within a line.
    private static (string Text, List<(int Offset, int Length, TextPointer Pointer)> Anchors) BuildTextSnapshot(
        FlowDocument document)
    {
        var builder = new StringBuilder();
        var anchors = new List<(int, int, TextPointer)>();
        var pointer = document.ContentStart;

        while (pointer is not null)
        {
            switch (pointer.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                    var runText = pointer.GetTextInRun(LogicalDirection.Forward);
                    anchors.Add((builder.Length, runText.Length, pointer));
                    builder.Append(runText);
                    pointer = pointer.GetPositionAtOffset(runText.Length, LogicalDirection.Forward);
                    break;

                case TextPointerContext.ElementStart:
                case TextPointerContext.ElementEnd:
                    if (pointer.GetAdjacentElement(LogicalDirection.Forward) is Block or LineBreak
                        && builder.Length > 0 && builder[^1] != '\n')
                    {
                        builder.Append('\n');
                    }

                    pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
                    break;

                default:
                    pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
                    break;
            }
        }

        return (builder.ToString(), anchors);
    }
}
