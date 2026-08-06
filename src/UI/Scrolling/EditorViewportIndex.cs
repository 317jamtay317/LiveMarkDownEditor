using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace UI.Scrolling;

/// <summary>
/// The Viewport Slice index an overlay keeps over its own document-ordered set — Code Regions,
/// Misspellings, Matches, changed Blocks. It answers, on every repaint, which members the Viewport
/// reaches, so the overlay resolves text layout only for those and not for the whole document
/// (INV-074).
/// </summary>
/// <remarks>
/// Rebuilding the index costs no text measurement at all: it only compares <see cref="TextPointer"/>s,
/// which read the document's structure rather than its layout. The single measured operation is
/// locating the Viewport's own edges, which is two hit-tests per repaint however long the document is.
/// <para>
/// A set is expected in document order, which is how every caller builds it — by walking the document.
/// The two sequences the search needs are then non-decreasing: each member's start, and the furthest
/// end reached by it or anything before it. That running maximum is what keeps a span taller than the
/// Viewport in its own slice, since such a span has neither edge on screen.
/// </para>
/// </remarks>
internal sealed class EditorViewportIndex
{
    private TextPointer[] _starts = [];
    private TextPointer[] _furthestEnds = [];

    /// <summary>How many members the index currently covers.</summary>
    public int Count => _starts.Length;

    /// <summary>Rebuilds the index over <paramref name="ranges"/>, which must be in document order.</summary>
    /// <param name="ranges">The spans the overlay draws.</param>
    public void Rebuild(IReadOnlyList<TextRange> ranges) =>
        Rebuild(ranges, static range => range.Start, static range => range.End);

    /// <summary>
    /// Rebuilds the index over <paramref name="items"/>, which must be in document order, reading each
    /// member's span through the supplied accessors.
    /// </summary>
    /// <typeparam name="T">What the overlay holds — a range, a region, a highlight target.</typeparam>
    /// <param name="items">The members the overlay draws.</param>
    /// <param name="startOf">Reads a member's start position.</param>
    /// <param name="endOf">Reads a member's end position.</param>
    public void Rebuild<T>(IReadOnlyList<T> items, Func<T, TextPointer> startOf, Func<T, TextPointer> endOf)
    {
        var count = items.Count;
        var starts = new TextPointer[count];
        var furthestEnds = new TextPointer[count];

        try
        {
            TextPointer? furthest = null;
            for (var index = 0; index < count; index++)
            {
                starts[index] = startOf(items[index]);
                var end = endOf(items[index]);
                furthest = furthest is null || end.CompareTo(furthest) > 0 ? end : furthest;
                furthestEnds[index] = furthest;
            }

            _starts = starts;
            _furthestEnds = furthestEnds;
        }
        catch (InvalidOperationException)
        {
            // Positions left over from a document that has just been replaced. Index nothing rather than
            // half of something; whatever rescan is already pending rebuilds against the new document.
            _starts = [];
            _furthestEnds = [];
        }
    }

    /// <summary>Forgets everything, so nothing is drawn until the next rebuild.</summary>
    public void Clear()
    {
        _starts = [];
        _furthestEnds = [];
    }

    /// <summary>
    /// The run of members <paramref name="editor"/>'s Viewport reaches.
    /// </summary>
    /// <param name="editor">The editing surface whose Viewport bounds the repaint.</param>
    /// <param name="expectedCount">
    /// How many members the caller holds. A mismatch means the index is stale against the set it
    /// describes, which yields an empty slice rather than an index out of range.
    /// </param>
    /// <param name="layoutQueries">Incremented by the measured operations performed, for the profiler.</param>
    /// <returns>The slice to draw, which may be empty.</returns>
    public ViewportSlice Slice(Control editor, int expectedCount, ref int layoutQueries)
    {
        if (expectedCount == 0 || _starts.Length != expectedCount)
        {
            return ViewportSlice.Empty;
        }

        var viewportHeight = editor.ActualHeight;
        if (viewportHeight <= 0)
        {
            return ViewportSlice.Empty;
        }

        try
        {
            layoutQueries += 2;
            var top = PositionAt(editor, 0d);
            var bottom = PositionAt(editor, viewportHeight);
            if (top is null || bottom is null)
            {
                // Nothing laid out to hit-test yet. Draw the lot rather than nothing, and let the next
                // repaint — which will have a layout — narrow it.
                return new ViewportSlice(0, expectedCount);
            }

            return ExtentIndex.Of(_starts, _furthestEnds, top, bottom, static (a, b) => a.CompareTo(b));
        }
        catch (InvalidOperationException)
        {
            return ViewportSlice.Empty;
        }
    }

    private static TextPointer? PositionAt(Control editor, double y) => editor switch
    {
        RichTextBox rich => rich.GetPositionFromPoint(new Point(0, y), snapToText: true),
        _ => null,
    };
}
