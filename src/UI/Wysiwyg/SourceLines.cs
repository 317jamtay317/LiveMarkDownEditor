using System.Windows;

namespace UI.Wysiwyg;

/// <summary>
/// The range of Markdown source lines one Block of a Visual Document was projected from — where in
/// the source text the reader is looking.
/// </summary>
/// <remarks>
/// Both ends are inclusive and 0-based, so a block occupying a single source line has equal
/// <see cref="StartLine"/> and <see cref="EndLine"/>. This is a value object.
/// </remarks>
/// <param name="StartLine">The 0-based source line the block begins on.</param>
/// <param name="EndLine">The 0-based source line the block ends on; never before the start.</param>
public sealed record SourceLineRange(int StartLine, int EndLine)
{
    /// <summary>Whether this range shares any source line with the lines <c>[start, start + count)</c>.</summary>
    /// <param name="start">The 0-based first line of the run to test.</param>
    /// <param name="count">How many lines the run covers; a run of none intersects nothing.</param>
    /// <returns><see langword="true"/> when the two overlap by at least one line.</returns>
    public bool Intersects(int start, int count) =>
        count > 0 && StartLine <= start + count - 1 && EndLine >= start;
}

/// <summary>
/// Records, on each Block of a Visual Document, the Source Line Range it was projected from — an
/// attached property rather than the <c>Tag</c>, which already carries the block's Markdown role.
/// </summary>
/// <remarks>
/// It is what lets a line-based comparison of source text be shown against the Visual Document: the
/// Change Highlight resolves the Changed Regions of a Reload Difference to Blocks through this
/// (INV-060). It is presentation metadata — Capture never reads it, so it cannot reach the Markdown
/// Document, and recording it leaves Project a pure function of its inputs (INV-003).
/// </remarks>
public static class SourceLines
{
    /// <summary>Identifies the attached Source Line Range property.</summary>
    public static readonly DependencyProperty RangeProperty = DependencyProperty.RegisterAttached(
        "Range",
        typeof(SourceLineRange),
        typeof(SourceLines),
        new PropertyMetadata(null));

    /// <summary>Records the Source Line Range <paramref name="element"/> was projected from.</summary>
    /// <param name="element">The Block of the Visual Document to record against.</param>
    /// <param name="value">The source lines it came from, or <see langword="null"/> for a block the
    /// projection introduced rather than read (the trailing paragraph after a Block Island).</param>
    public static void SetRange(DependencyObject element, SourceLineRange? value)
    {
        ArgumentNullException.ThrowIfNull(element, nameof(element));
        element.SetValue(RangeProperty, value);
    }

    /// <summary>Reads the Source Line Range <paramref name="element"/> was projected from.</summary>
    /// <param name="element">The Block of the Visual Document to read from.</param>
    /// <returns>Its source lines, or <see langword="null"/> when it came from none.</returns>
    public static SourceLineRange? GetRange(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element, nameof(element));
        return (SourceLineRange?)element.GetValue(RangeProperty);
    }

}

/// <summary>
/// The line structure of one Markdown source text, built once so every block's Source Line Range can
/// be resolved from its character span without re-scanning the text.
/// </summary>
/// <remarks>
/// Project asks for one range per block; scanning the text for each would make projecting a large
/// document quadratic in its length, so the newline positions are indexed up front and each lookup
/// is a binary search over them.
/// </remarks>
public sealed class SourceLineIndex
{
    private readonly int[] _lineStarts;
    private readonly int _length;

    /// <summary>Indexes the lines of <paramref name="text"/>.</summary>
    /// <param name="text">The Markdown source text whose lines are indexed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public SourceLineIndex(string text)
    {
        ArgumentNullException.ThrowIfNull(text, nameof(text));

        _length = text.Length;
        var starts = new List<int> { 0 };
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                starts.Add(index + 1);
            }
        }

        _lineStarts = [.. starts];
    }

    /// <summary>
    /// Maps a Markdig block's reported position to its Source Line Range. The start line the parser
    /// reports is used directly; the end is found from the block's span, so a block spanning several
    /// source lines (a fenced Code Block, a list, a table) reports all of them.
    /// </summary>
    /// <param name="startLine">The 0-based line the parser reports the block starting on.</param>
    /// <param name="spanEnd">The character offset of the last character of the block's span.</param>
    /// <returns>The block's Source Line Range, never ending before it starts.</returns>
    public SourceLineRange RangeOf(int startLine, int spanEnd)
    {
        var start = Math.Max(0, startLine);
        return new SourceLineRange(start, Math.Max(start, LineOf(spanEnd)));
    }

    // The 0-based line holding the character at `offset`; offsets outside the text clamp to its
    // first or last line.
    private int LineOf(int offset)
    {
        var clamped = Math.Min(Math.Max(offset, 0), Math.Max(0, _length - 1));
        var found = Array.BinarySearch(_lineStarts, clamped);
        return found >= 0 ? found : ~found - 1;
    }
}
