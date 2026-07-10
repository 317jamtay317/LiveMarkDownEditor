using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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
/// theme recolour stays cheap: recolouring a brush that backs text forces WPF to re-format that text,
/// which on a code-heavy document reflows the whole document. Filling a rectangle in an adorner that
/// owns no text only repaints. The adorner re-scans for Code Regions when the document changes; a
/// scroll or resize merely repaints from the regions already held.
/// </remarks>
public sealed class CodeShadingAdorner : Adorner
{
    // A Code Block's shade is inset to cover its Padding; an inline Code Span's hugs the text closely.
    private const double BlockPadding = 8d;
    private const double SpanPadding = 2d;
    private const double RightInset = 14d;
    private const double CornerRadius = 3d;

    private readonly RichTextBox _editor;
    private IReadOnlyList<CodeRegion> _regions = [];

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

        var viewportWidth = _editor.ActualWidth;
        var viewportHeight = _editor.ActualHeight;
        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, viewportWidth, viewportHeight)));

        foreach (var region in _regions)
        {
            if (region.IsBlock)
            {
                DrawBlock(drawingContext, region, brush, viewportWidth, viewportHeight);
            }
            else
            {
                DrawSpan(drawingContext, region, brush, viewportHeight);
            }
        }

        drawingContext.Pop();
    }

    // A Code Block: a full-width panel from the first line to the last, inset to cover the block's
    // Padding on the left and top/bottom. Two character rects (start, end) give its vertical extent, so
    // even a block taller than the viewport is drawn (and clipped) without walking every line.
    private void DrawBlock(DrawingContext drawingContext, CodeRegion region, Brush brush, double viewportWidth, double viewportHeight)
    {
        try
        {
            var startRect = region.Start.GetCharacterRect(LogicalDirection.Forward);
            var endRect = region.End.GetCharacterRect(LogicalDirection.Backward);
            if (startRect == Rect.Empty || endRect == Rect.Empty)
            {
                return;
            }

            if (endRect.Bottom < 0 || startRect.Top > viewportHeight)
            {
                return;
            }

            var left = startRect.Left - BlockPadding;
            var right = viewportWidth - RightInset;
            if (right <= left)
            {
                return;
            }

            var box = new Rect(left, startRect.Top - BlockPadding, right - left, endRect.Bottom - startRect.Top + (2 * BlockPadding));
            drawingContext.DrawRoundedRectangle(brush, null, box, CornerRadius, CornerRadius);
        }
        catch (InvalidOperationException)
        {
            // A pointer left over from a document just replaced — ignore; the pending rescan rebuilds.
        }
    }

    // An inline Code Span: a snug box hugging the text, one per visual line so a span that wraps at a
    // line end is shaded on both lines.
    private void DrawSpan(DrawingContext drawingContext, CodeRegion region, Brush brush, double viewportHeight)
    {
        try
        {
            foreach (var line in LineBoxes(region.Start, region.End))
            {
                if (line.Bottom < 0 || line.Top > viewportHeight || line.Width < 1)
                {
                    continue;
                }

                var box = new Rect(line.Left - SpanPadding, line.Top, line.Width + (2 * SpanPadding), line.Height);
                drawingContext.DrawRoundedRectangle(brush, null, box, CornerRadius, CornerRadius);
            }
        }
        catch (InvalidOperationException)
        {
            // As above — a stale pointer during a document swap.
        }
    }

    // Splits a range into one box per visual line by walking its insertion positions and grouping the
    // caret rectangles by line. Bounded by the range length, so it is only used for short Code Spans.
    private static IReadOnlyList<Rect> LineBoxes(TextPointer start, TextPointer end)
    {
        var boxes = new List<Rect>();
        double top = double.NaN, left = 0, right = 0, height = 0;

        var position = start;
        var guard = 0;
        while (position is not null && position.CompareTo(end) <= 0 && guard++ < 4000)
        {
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
