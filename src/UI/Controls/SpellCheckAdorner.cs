using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using UI.Spelling;

namespace UI.Controls;

/// <summary>
/// Draws red squiggles under the misspelled words of a <see cref="RichTextBox"/>'s Visual Document,
/// replacing WPF's built-in spell check. Because it segments text with the camelCase-aware
/// <see cref="WordTokenizer"/> and checks each sub-word individually, only a genuinely misspelled
/// part of a code-like identifier is underlined (e.g. only <c>Invld</c> in <c>this.ShouldBe().Invld()</c>).
/// Runs marked as code (Language <c>zxx</c>) are skipped entirely.
/// </summary>
/// <remarks>
/// Spell checking (the tokenize-and-look-up pass) runs only after edits settle, debounced, and its
/// results are held as <see cref="TextRange"/>s. Scrolling and resizing merely repaint from those
/// ranges, so neither re-runs the dictionary — keeping scrolling cheap.
/// </remarks>
public sealed class SpellCheckAdorner : Adorner
{
    // "zxx" (no linguistic content) is the tag the projector puts on code, so we never spell-check it.
    private const string CodeLanguageTag = "zxx";

    private static readonly Pen SquigglePen = CreateSquigglePen();

    private readonly RichTextBox _editor;
    private readonly ISpellDictionary _dictionary;
    private readonly DispatcherTimer _debounce;
    private readonly List<TextRange> _misspellings = [];

    /// <summary>Creates the adorner over <paramref name="editor"/> and begins watching for edits.</summary>
    /// <param name="editor">The editor whose Visual Document is spell-checked.</param>
    /// <param name="dictionary">The dictionary used to judge each word.</param>
    public SpellCheckAdorner(RichTextBox editor, ISpellDictionary dictionary)
        : base(editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        IsHitTestVisible = false;

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Rescan();
        };

        _editor.TextChanged += OnTextChanged;
        _editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrolled));
        _editor.SizeChanged += OnScrolled;

        QueueRescan();
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        // The prior ranges belong to the outgoing document; drop them at once so a repaint before the
        // rescan never touches a stale pointer, then re-scan once edits settle.
        _misspellings.Clear();
        InvalidateVisual();
        QueueRescan();
    }

    private void OnScrolled(object? sender, EventArgs e) => InvalidateVisual();

    private void QueueRescan()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void Rescan()
    {
        _misspellings.Clear();
        if (_editor.Document is { } document)
        {
            foreach (var run in ProseRuns(document.Blocks))
            {
                foreach (var word in SpellCheckScanner.FindMisspellings(run.Text, _dictionary))
                {
                    var range = RangeFor(run, word);
                    if (range is not null)
                    {
                        _misspellings.Add(range);
                    }
                }
            }
        }

        InvalidateVisual();
    }

    private static TextRange? RangeFor(Run run, Word word)
    {
        var start = run.ContentStart.GetPositionAtOffset(word.Start, LogicalDirection.Forward);
        var end = start?.GetPositionAtOffset(word.Length, LogicalDirection.Forward);
        return start is null || end is null ? null : new TextRange(start, end);
    }

    // Every text Run of the document except those marked as code (Language zxx), walking into the
    // block and inline containers the projector produces (sections, lists, tables, spans).
    private static IEnumerable<Run> ProseRuns(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph when !IsCode(paragraph):
                    foreach (var run in ProseRunsIn(paragraph.Inlines))
                    {
                        yield return run;
                    }

                    break;

                case Section section:
                    foreach (var run in ProseRuns(section.Blocks))
                    {
                        yield return run;
                    }

                    break;

                case List list:
                    foreach (var run in list.ListItems.SelectMany(item => ProseRuns(item.Blocks)))
                    {
                        yield return run;
                    }

                    break;

                case Table table:
                    foreach (var run in table.RowGroups
                        .SelectMany(group => group.Rows)
                        .SelectMany(row => row.Cells)
                        .SelectMany(cell => ProseRuns(cell.Blocks)))
                    {
                        yield return run;
                    }

                    break;
            }
        }
    }

    private static IEnumerable<Run> ProseRunsIn(IEnumerable<Inline> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run when !IsCode(run):
                    yield return run;
                    break;
                case Span span:
                    foreach (var nested in ProseRunsIn(span.Inlines))
                    {
                        yield return nested;
                    }

                    break;
            }
        }
    }

    private static bool IsCode(TextElement element) => element.Language?.IetfLanguageTag == CodeLanguageTag;

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_misspellings.Count == 0)
        {
            return;
        }

        var viewportHeight = _editor.ActualHeight;
        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, _editor.ActualWidth, viewportHeight)));
        foreach (var range in _misspellings)
        {
            DrawSquiggle(drawingContext, range, viewportHeight);
        }

        drawingContext.Pop();
    }

    private static void DrawSquiggle(DrawingContext drawingContext, TextRange range, double viewportHeight)
    {
        try
        {
            var startRect = range.Start.GetCharacterRect(LogicalDirection.Forward);
            var endRect = range.End.GetCharacterRect(LogicalDirection.Backward);
            if (startRect == Rect.Empty || endRect == Rect.Empty)
            {
                return;
            }

            // Only the same-line, in-viewport case; a wrapped word (rare for an identifier) is skipped.
            if (Math.Abs(startRect.Top - endRect.Top) > 0.5 || startRect.Bottom < 0 || startRect.Top > viewportHeight)
            {
                return;
            }

            var left = startRect.Left;
            var right = endRect.Right;
            var y = startRect.Bottom - 1;
            if (right - left < 1)
            {
                return;
            }

            drawingContext.DrawGeometry(null, SquigglePen, BuildSquiggle(left, right, y));
        }
        catch (InvalidOperationException)
        {
            // A pointer left over from a document that has just been replaced — ignore; the pending
            // rescan will rebuild the ranges against the new document.
        }
    }

    private static Geometry BuildSquiggle(double left, double right, double y)
    {
        const double step = 2d;
        const double amplitude = 1.5d;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(left, y), isFilled: false, isClosed: false);
            var up = true;
            for (var x = left + step; x <= right; x += step)
            {
                context.LineTo(new Point(x, up ? y - amplitude : y), isStroked: true, isSmoothJoin: false);
                up = !up;
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static Pen CreateSquigglePen()
    {
        var pen = new Pen(Brushes.Red, 1d);
        pen.Freeze();
        return pen;
    }
}
