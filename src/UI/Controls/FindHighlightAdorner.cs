using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace UI.Controls;

/// <summary>
/// Draws the Find highlights over a <see cref="RichTextBox"/>'s Visual Document: a translucent fill
/// behind every Match, with the Current Match filled more strongly and outlined. It is a read-only
/// overlay — it only paints from the Match ranges the editor hands it — so Find never changes the
/// Markdown Document (INV-016).
/// </summary>
/// <remarks>
/// The editor owns the Find computation and calls <see cref="Update"/> with the Match
/// <see cref="TextRange"/>s and the Current Match index whenever they change. Scrolling and resizing
/// only repaint from those ranges; they never recompute Matches.
/// </remarks>
public sealed class FindHighlightAdorner : Adorner
{
    private readonly RichTextBox _editor;
    private IReadOnlyList<TextRange> _matches = [];
    private int _currentIndex = -1;

    private Brush _matchBrush = Brushes.Transparent;
    private Brush _currentBrush = Brushes.Transparent;
    private Pen? _currentOutline;

    /// <summary>Creates the adorner over <paramref name="editor"/>, repainting as it scrolls or resizes.</summary>
    /// <param name="editor">The editor whose Matches are highlighted.</param>
    public FindHighlightAdorner(RichTextBox editor)
        : base(editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        IsHitTestVisible = false;

        _editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnRepaintNeeded));
        _editor.SizeChanged += OnRepaintNeeded;
    }

    /// <summary>
    /// Sets the highlight colours, so the overlay follows the active light/dark palette. The Match
    /// fill sits behind all Matches; the Current-Match fill and outline mark the one in focus.
    /// </summary>
    /// <param name="matchBrush">The fill behind every Match.</param>
    /// <param name="currentBrush">The fill behind the Current Match.</param>
    /// <param name="currentOutline">The outline around the Current Match.</param>
    public void SetColors(Brush matchBrush, Brush currentBrush, Pen currentOutline)
    {
        _matchBrush = matchBrush;
        _currentBrush = currentBrush;
        _currentOutline = currentOutline;
        InvalidateVisual();
    }

    private void OnRepaintNeeded(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>Replaces the highlighted Matches and which one is the Current Match, then repaints.</summary>
    /// <param name="matches">The Match ranges to highlight, in document order.</param>
    /// <param name="currentIndex">The index of the Current Match, or <c>-1</c> when there is none.</param>
    public void Update(IReadOnlyList<TextRange> matches, int currentIndex)
    {
        _matches = matches;
        _currentIndex = currentIndex;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_matches.Count == 0)
        {
            return;
        }

        var viewportHeight = _editor.ActualHeight;
        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, _editor.ActualWidth, viewportHeight)));
        for (var index = 0; index < _matches.Count; index++)
        {
            var isCurrent = index == _currentIndex;
            DrawHighlight(drawingContext, _matches[index], viewportHeight, isCurrent);
        }

        drawingContext.Pop();
    }

    private void DrawHighlight(DrawingContext drawingContext, TextRange range, double viewportHeight, bool isCurrent)
    {
        try
        {
            var startRect = range.Start.GetCharacterRect(LogicalDirection.Forward);
            var endRect = range.End.GetCharacterRect(LogicalDirection.Backward);
            if (startRect == Rect.Empty || endRect == Rect.Empty)
            {
                return;
            }

            // Highlight only same-line, in-viewport Matches; a wrapped Match (rare for a short query)
            // is skipped rather than painted as a giant band.
            if (Math.Abs(startRect.Top - endRect.Top) > 0.5 || startRect.Bottom < 0 || startRect.Top > viewportHeight)
            {
                return;
            }

            var left = startRect.Left;
            var right = endRect.Right;
            if (right - left < 1)
            {
                return;
            }

            var box = new Rect(left - 1, startRect.Top, right - left + 2, startRect.Height);
            var fill = isCurrent ? _currentBrush : _matchBrush;
            drawingContext.DrawRoundedRectangle(fill, isCurrent ? _currentOutline : null, box, 2, 2);
        }
        catch (InvalidOperationException)
        {
            // A pointer left over from a document that has just been replaced — ignore; the editor
            // recomputes the Match ranges against the new document.
        }
    }
}
