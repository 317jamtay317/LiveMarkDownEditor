using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using UI.Diagnostics;
using UI.Scrolling;
using UI.Wysiwyg;

namespace UI.Controls;

/// <summary>
/// Draws the Code Shading over a <see cref="RichTextBox"/>'s Visual Document: a subtle shaded panel
/// behind every Code Block and Code Span, so code is set off from prose. It is a read-only overlay —
/// it only paints behind the Code Regions the <see cref="CodeShadingScanner"/> finds — so shading
/// code never changes the Markdown Document (INV-017).
/// </summary>
/// <remarks>
/// The shade is an <em>overlay</em>, not each code element's own <c>Background</c>, precisely so a
/// theme recolor stays cheap: recoloring a brush that backs text forces WPF to re-format that text,
/// which on a code-heavy document reflows the whole document. Filling a rectangle in an adorner that
/// owns no text only repaints. The adorner re-scans for Code Regions when the document changes; a
/// scroll or resize merely repaints from the regions already held.
/// </remarks>
public sealed class CodeShadingAdorner : Adorner
{
    // A Code Block's shade is inset to cover its Padding; an inline Code Span's hugs the text closely.
    private const double BlockPadding = 8d;
    private const double SpanPadding = 2d;
    private const double CornerRadius = 3d;

    private readonly RichTextBox _editor;
    private IReadOnlyList<CodeRegion> _regions = [];

    // The Viewport Slice index over the Code Regions, so a repaint measures only the shading on screen
    // and a Code Block taller than the Viewport keeps its shade while the user reads its middle
    // (INV-074).
    private readonly EditorViewportIndex _onScreen = new();

    // Counts the layout resolutions of the render in progress, for ScrollProfiler.
    private int _layoutQueries;

    /// <summary>Creates the adorner over <paramref name="editor"/>, re-scanning on edits and repainting on scroll or resize.</summary>
    /// <param name="editor">The editor whose Code Regions are shaded.</param>
    public CodeShadingAdorner(RichTextBox editor)
        : base(editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        IsHitTestVisible = false;

        _editor.TextChanged += OnDocumentChanged;
        _editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnRepaintNeeded));
        _editor.SizeChanged += OnRepaintNeeded;

        Rescan();
    }

    private void OnDocumentChanged(object? sender, EventArgs e) => Rescan();

    private void OnRepaintNeeded(object? sender, EventArgs e) => InvalidateVisual();

    // Rebuilds the Code Regions from the current Visual Document, then repaints. Cheap — it walks the
    // block/inline tree by tag, doing no text measurement (that happens per-region at render time).
    private void Rescan()
    {
        _regions = CodeShadingScanner.Scan(_editor.Document);
        BuildSliceIndex();
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_regions.Count == 0 || _editor.TryFindResource("CodeShadingBrush") is not Brush brush)
        {
            return;
        }

        var stopwatch = ScrollProfiler.Start();
        _layoutQueries = 0;
        var drawn = 0;

        var viewportWidth = _editor.ActualWidth;
        var viewportHeight = _editor.ActualHeight;
        var slice = _onScreen.Slice(_editor, _regions.Count, ref _layoutQueries);
        if (slice.IsEmpty)
        {
            ScrollProfiler.Record(stopwatch, "CodeShadingAdorner.OnRender", 0, 0, _layoutQueries);
            return;
        }

        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, viewportWidth, viewportHeight)));

        for (var index = slice.First; index <= slice.Last; index++)
        {
            var region = _regions[index];
            var painted = region.IsBlock
                ? DrawBlock(drawingContext, region, brush, viewportHeight)
                : DrawSpan(drawingContext, region, brush, viewportHeight);
            if (painted)
            {
                drawn++;
            }
        }

        drawingContext.Pop();
        ScrollProfiler.Record(stopwatch, "CodeShadingAdorner.OnRender", slice.Count, drawn, _layoutQueries);
    }

    // The scanner yields Code Regions in document order, which is what the slice index expects.
    private void BuildSliceIndex() =>
        _onScreen.Rebuild(_regions, static region => region.Start, static region => region.End);

    // A Code Block: a panel spanning the text column, from the first line to the last, inset to cover
    // the block's Padding on the left and top/bottom. Two character rects (start, end) give its
    // vertical extent, so even a block taller than the viewport is drawn (and clipped) without walking
    // every line. It stops at the column rather than at the control's edge, so the shade is inset
    // evenly on both sides — see EditorTextColumn.
    private bool DrawBlock(DrawingContext drawingContext, CodeRegion region, Brush brush, double viewportHeight)
    {
        try
        {
            _layoutQueries += 2;
            var startRect = region.Start.GetCharacterRect(LogicalDirection.Forward);
            var endRect = region.End.GetCharacterRect(LogicalDirection.Backward);
            if (startRect == Rect.Empty || endRect == Rect.Empty)
            {
                return false;
            }

            if (endRect.Bottom < 0 || startRect.Top > viewportHeight)
            {
                return false;
            }

            var left = startRect.Left - BlockPadding;
            var right = EditorTextColumn.RightEdge(_editor);
            if (right <= left)
            {
                return false;
            }

            var box = new Rect(left, startRect.Top - BlockPadding, right - left, endRect.Bottom - startRect.Top + (2 * BlockPadding));
            drawingContext.DrawRoundedRectangle(brush, null, box, CornerRadius, CornerRadius);
            return true;
        }
        catch (InvalidOperationException)
        {
            // A pointer left over from a document just replaced — ignore; the pending rescan rebuilds.
            return false;
        }
    }

    // An inline Code Span: a snug box hugging the text, one per visual line so a span that wraps at a
    // line end is shaded on both lines.
    //
    // Nearly every Code Span sits on one line, and such a span is fully described by its two ends —
    // two layout resolutions, whatever its length. Only a span that actually wraps needs the walk,
    // and only then is the walk's cost paid (INV-074).
    private bool DrawSpan(DrawingContext drawingContext, CodeRegion region, Brush brush, double viewportHeight)
    {
        try
        {
            _layoutQueries += 2;
            var startRect = region.Start.GetCharacterRect(LogicalDirection.Forward);
            var endRect = region.End.GetCharacterRect(LogicalDirection.Backward);

            if (CodeSpanShade.Box(startRect, endRect, SpanPadding, viewportHeight) is { } box)
            {
                drawingContext.DrawRoundedRectangle(brush, null, box, CornerRadius, CornerRadius);
                return true;
            }

            // Nothing to draw as one box. Either the span is off screen or unresolved — in which case
            // the walk would find nothing either and is skipped — or it wraps, which is what the walk
            // is for.
            if (startRect == Rect.Empty || endRect == Rect.Empty
                || Math.Abs(startRect.Top - endRect.Top) <= 0.5d
                || Math.Max(startRect.Bottom, endRect.Bottom) < 0d
                || Math.Min(startRect.Top, endRect.Top) > viewportHeight)
            {
                return false;
            }

            return DrawWrappedSpan(drawingContext, region, brush, viewportHeight);
        }
        catch (InvalidOperationException)
        {
            // As above — a stale pointer during a document swap.
            return false;
        }
    }

    // The fallback for a Code Span that wraps across visual lines: one box per line.
    private bool DrawWrappedSpan(DrawingContext drawingContext, CodeRegion region, Brush brush, double viewportHeight)
    {
        try
        {
            var painted = false;
            foreach (var line in LineBoxes(region.Start, region.End))
            {
                if (line.Bottom < 0 || line.Top > viewportHeight || line.Width < 1)
                {
                    continue;
                }

                var box = new Rect(line.Left - SpanPadding, line.Top, line.Width + (2 * SpanPadding), line.Height);
                drawingContext.DrawRoundedRectangle(brush, null, box, CornerRadius, CornerRadius);
                painted = true;
            }

            return painted;
        }
        catch (InvalidOperationException)
        {
            // As above — a stale pointer during a document swap.
            return false;
        }
    }

    // Splits a range into one box per visual line by walking its insertion positions and grouping the
    // caret rectangles by line. Bounded by the range length, so it is only used for short Code Spans.
    private IReadOnlyList<Rect> LineBoxes(TextPointer start, TextPointer end)
    {
        var boxes = new List<Rect>();
        double top = double.NaN, left = 0, right = 0, height = 0;

        var position = start;
        var guard = 0;
        while (position is not null && position.CompareTo(end) <= 0 && guard++ < 4000)
        {
            _layoutQueries++;
            var rect = position.GetCharacterRect(LogicalDirection.Forward);
            if (rect != Rect.Empty)
            {
                if (double.IsNaN(top) || Math.Abs(rect.Top - top) > 0.5)
                {
                    if (!double.IsNaN(top))
                    {
                        boxes.Add(new Rect(left, top, Math.Max(0, right - left), height));
                    }

                    top = rect.Top;
                    left = rect.Left;
                    right = rect.Left;
                    height = rect.Height;
                }
                else
                {
                    left = Math.Min(left, rect.Left);
                    right = Math.Max(right, rect.Left);
                    height = Math.Max(height, rect.Height);
                }
            }

            if (position.CompareTo(end) == 0)
            {
                break;
            }

            var next = position.GetNextInsertionPosition(LogicalDirection.Forward);
            if (next is null || next.CompareTo(position) == 0)
            {
                break;
            }

            position = next.CompareTo(end) > 0 ? end : next;
        }

        if (!double.IsNaN(top))
        {
            boxes.Add(new Rect(left, top, Math.Max(0, right - left), height));
        }

        return boxes;
    }
}
