using System.Windows;

namespace UI.Wysiwyg;

/// <summary>
/// The shade drawn behind an inline Code Span, derived from the two ends of the span alone. A Code
/// Span that did not wrap is one box, so a repaint resolves two character rectangles for it rather
/// than one per character it contains (INV-074).
/// </summary>
/// <remarks>
/// Pure geometry over rectangles rather than over <c>TextPointer</c>s, so the rules the shade obeys
/// are testable without a laid-out document. The caller resolves the two ends — the only measured
/// operation — and this decides what, if anything, to draw.
/// <para>
/// The Viewport check happens here, on the two end rectangles, rather than in the caller after a walk:
/// culling a span only once its every character has been measured is not culling at all, which is the
/// first rule INV-074 states.
/// </para>
/// </remarks>
public static class CodeSpanShade
{
    // Two ends within this of each other vertically are on the same visual line. A caret rectangle's
    // top is exact, so the tolerance only absorbs sub-pixel layout rounding.
    private const double SameLineTolerance = 0.5d;

    // Narrower than this and there is nothing worth drawing — an empty span, or two ends that resolved
    // to the same place.
    private const double MinimumWidth = 1d;

    /// <summary>
    /// The box to shade for a Code Span whose ends resolved to <paramref name="start"/> and
    /// <paramref name="end"/>.
    /// </summary>
    /// <param name="start">The character rectangle at the span's start.</param>
    /// <param name="end">The character rectangle at the span's end.</param>
    /// <param name="padding">How far the shade extends beyond the text on each side.</param>
    /// <param name="viewportHeight">The height of the Viewport, for the off-screen cull.</param>
    /// <returns>
    /// The box to draw, or <see langword="null"/> when there is nothing to draw: either end
    /// unresolved, the span wholly above or below the Viewport, the two ends on different visual lines
    /// (a wrapped span, which the caller shades line by line instead), or a degenerate width.
    /// </returns>
    public static Rect? Box(Rect start, Rect end, double padding, double viewportHeight)
    {
        if (start == Rect.Empty || end == Rect.Empty)
        {
            return null;
        }

        // Off screen entirely — refused before anything is computed from it.
        if (Math.Max(start.Bottom, end.Bottom) < 0d || Math.Min(start.Top, end.Top) > viewportHeight)
        {
            return null;
        }

        // Wrapped: the two ends are on different visual lines, so no single box covers the span. The
        // caller falls back to walking it line by line, which is worth its cost only in this case.
        if (Math.Abs(start.Top - end.Top) > SameLineTolerance)
        {
            return null;
        }

        // Min/Max rather than assuming the end is to the right: a degenerate or bidi-reversed span can
        // put it to the left, and a negative width throws inside Rect — during a render, which is fatal.
        var left = Math.Min(start.Left, end.Left) - padding;
        var right = Math.Max(start.Right, end.Right) + padding;
        var width = right - left;
        if (width < MinimumWidth)
        {
            return null;
        }

        var top = Math.Min(start.Top, end.Top);
        var height = Math.Max(start.Height, end.Height);
        return new Rect(left, top, width, height);
    }
}
