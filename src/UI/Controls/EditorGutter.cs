using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UI.Diagnostics;

namespace UI.Controls;

/// <summary>
/// The Editor Gutter: the presentation-only margin strip drawn to the left of a
/// <see cref="MarkdownRichEditor"/>. It shows a Line Number for each visible line of the Visual
/// Document and a Fold Toggle (a chevron) beside each Section Heading that Folds or Unfolds that
/// Section when clicked.
/// </summary>
/// <remarks>
/// Authored as a custom Control (this class plus a ResourceDictionary for its look) — the only place
/// interaction logic lives outside a ViewModel. The gutter never mutates the document: it reads the
/// editor's blocks, fold state, and line layout and mirrors them, so it cannot change the Markdown
/// source (INV-011). Glyph positions come from <see cref="TextPointer.GetCharacterRect"/>, which
/// reports line positions relative to the editor's viewport; the gutter re-lays out whenever the
/// editor's text, size, or scroll offset changes.
/// </remarks>
public sealed class EditorGutter : Canvas
{
    // Chevron pointing down when the Section is Unfolded, right when it is Folded (the fold convention
    // used by Visual Studio and VS Code).
    private const string ExpandedGlyph = "▾";
    private const string CollapsedGlyph = "▸";

    private const double NumberColumnWidth = 28d;
    private const double ChevronColumnWidth = 18d;
    private const double ChevronGap = 4d;
    private const double ChevronFontSize = 14d;

    /// <summary>Identifies the <see cref="Editor"/> dependency property.</summary>
    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor),
        typeof(MarkdownRichEditor),
        typeof(EditorGutter),
        new PropertyMetadata(null, OnEditorChanged));

    /// <summary>Identifies the <see cref="LineNumberBrush"/> dependency property.</summary>
    public static readonly DependencyProperty LineNumberBrushProperty = DependencyProperty.Register(
        nameof(LineNumberBrush),
        typeof(Brush),
        typeof(EditorGutter),
        new PropertyMetadata(Brushes.Gray));

    /// <summary>Identifies the <see cref="ChevronBrush"/> dependency property.</summary>
    public static readonly DependencyProperty ChevronBrushProperty = DependencyProperty.Register(
        nameof(ChevronBrush),
        typeof(Brush),
        typeof(EditorGutter),
        new PropertyMetadata(Brushes.Gray));

    /// <summary>Identifies the <see cref="ChevronHoverBrush"/> dependency property.</summary>
    public static readonly DependencyProperty ChevronHoverBrushProperty = DependencyProperty.Register(
        nameof(ChevronHoverBrush),
        typeof(Brush),
        typeof(EditorGutter),
        new PropertyMetadata(Brushes.SteelBlue));

    // How far the gutter will walk from its remembered anchor before giving up and recounting from the
    // start of the document. A scroll moves a few lines, so the walk is normally a handful of steps;
    // a jump (Ctrl+End, Navigate to a heading) is what the recount is for.
    private const int MaxAnchorWalk = 400;

    private bool _refreshQueued;

    // The last line the gutter numbered, and the ordinal it gave it. Line Numbers count every rendered
    // line above them, which is the one thing that cannot be read off the Viewport — so it is
    // remembered between repaints and recounted only when the document's layout changes (INV-074).
    private TextPointer? _anchorLine;
    private int _anchorNumber;

    /// <summary>Initialises the gutter, refreshing once it is loaded into the visual tree.</summary>
    public EditorGutter()
    {
        Loaded += (_, _) => ScheduleRefresh();
    }

    /// <summary>The <see cref="MarkdownRichEditor"/> whose lines and Sections this gutter mirrors.</summary>
    public MarkdownRichEditor? Editor
    {
        get => (MarkdownRichEditor?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>The brush used to paint Line Numbers.</summary>
    public Brush LineNumberBrush
    {
        get => (Brush)GetValue(LineNumberBrushProperty);
        set => SetValue(LineNumberBrushProperty, value);
    }

    /// <summary>The brush used to paint a Fold Toggle chevron.</summary>
    public Brush ChevronBrush
    {
        get => (Brush)GetValue(ChevronBrushProperty);
        set => SetValue(ChevronBrushProperty, value);
    }

    /// <summary>The brush used to paint a Fold Toggle chevron while the pointer is over it.</summary>
    public Brush ChevronHoverBrush
    {
        get => (Brush)GetValue(ChevronHoverBrushProperty);
        set => SetValue(ChevronHoverBrushProperty, value);
    }

    private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var gutter = (EditorGutter)d;
        if (e.OldValue is MarkdownRichEditor oldEditor)
        {
            oldEditor.TextChanged -= gutter.OnEditorInvalidated;
            oldEditor.SizeChanged -= gutter.OnEditorInvalidated;
            oldEditor.Loaded -= gutter.OnEditorInvalidated;
            oldEditor.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(gutter.OnEditorScrolled));
        }

        if (e.NewValue is MarkdownRichEditor newEditor)
        {
            newEditor.TextChanged += gutter.OnEditorInvalidated;
            newEditor.SizeChanged += gutter.OnEditorInvalidated;
            newEditor.Loaded += gutter.OnEditorInvalidated;
            newEditor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(gutter.OnEditorScrolled));
        }

        gutter.ScheduleRefresh();
    }

    private void OnEditorInvalidated(object? sender, EventArgs e)
    {
        // A resize re-wraps the document, which renumbers every line below the change.
        _anchorLine = null;
        ScheduleRefresh();
    }

    // A scroll fires many ScrollChanged events in quick succession (a wheel notch or drag emits a
    // burst). Coalescing the rebuild to a single refresh per dispatcher cycle keeps scrolling smooth;
    // the numbers trail the content by at most a frame.
    private void OnEditorScrolled(object? sender, ScrollChangedEventArgs e)
    {
        // A change of extent or viewport means the document was edited or re-laid-out, so the
        // remembered line ordinal no longer describes it. A pure scroll leaves the numbering alone.
        if (e.ExtentHeightChange != 0d || e.ViewportHeightChange != 0d)
        {
            _anchorLine = null;
        }

        ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        Dispatcher.BeginInvoke(
            () =>
            {
                _refreshQueued = false;
                Refresh();
            },
            DispatcherPriority.Loaded);
    }

    private void Refresh()
    {
        Children.Clear();

        var editor = Editor;
        if (editor?.Document is null)
        {
            return;
        }

        var viewportHeight = editor.ActualHeight;
        if (viewportHeight <= 0)
        {
            return;
        }

        BuildLineNumbers(editor, viewportHeight);
        BuildFoldToggles(editor, viewportHeight);
    }

    // One Line Number per visible line of the Visual Document, counting each soft-wrapped
    // continuation as its own line the way a code editor does. The walk follows the editor's rendered
    // lines via GetLineStartPosition; the blocks of a Folded Section are absent from the document, so
    // the numbering naturally reflects only what is currently visible.
    private void BuildLineNumbers(MarkdownRichEditor editor, double viewportHeight)
    {
        var stopwatch = ScrollProfiler.Start();
        var walked = 0;
        var drawn = 0;
        var layoutQueries = 0;

        // Start at the Viewport rather than at the start of the document, so scrolling to the end of a
        // long document costs no more than scrolling to the beginning (INV-074).
        var first = FirstVisibleLine(editor, ref layoutQueries);
        if (first is null)
        {
            ScrollProfiler.Record(stopwatch, "EditorGutter.BuildLineNumbers", 0, 0, layoutQueries);
            return;
        }

        var fontSize = editor.FontSize > 0 ? editor.FontSize : 13d;
        var line = first.Value.Line;
        var number = first.Value.Number;
        while (line is not null)
        {
            walked++;
            layoutQueries++;
            var rect = line.GetCharacterRect(LogicalDirection.Forward);
            if (rect != Rect.Empty)
            {
                // Lines below the viewport are all lower still — stop walking.
                if (rect.Top > viewportHeight)
                {
                    break;
                }

                if (rect.Bottom >= 0)
                {
                    AddLineNumber(number, rect, fontSize, editor.FontFamily);
                    drawn++;
                }
            }

            line = line.GetLineStartPosition(1, out var moved);
            if (moved == 0)
            {
                break;
            }

            number++;
        }

        ScrollProfiler.Record(stopwatch, "EditorGutter.BuildLineNumbers", walked, drawn, layoutQueries);
    }

    // The first rendered line the Viewport shows, together with its Line Number. One hit-test finds the
    // line; the ordinal comes from the remembered anchor, which a scroll leaves only a few lines away.
    private (TextPointer Line, int Number)? FirstVisibleLine(MarkdownRichEditor editor, ref int layoutQueries)
    {
        layoutQueries++;
        var line = editor.GetPositionFromPoint(new Point(0, 0), snapToText: true)?.GetLineStartPosition(0);
        if (line is null)
        {
            return null;
        }

        var number = NumberOf(editor, line, ref layoutQueries);
        _anchorLine = line;
        _anchorNumber = number;
        return (line, number);
    }

    // The Line Number of a line: stepped from the remembered anchor when it is near enough, and counted
    // from the start of the document only when there is no usable anchor. The count itself never asks
    // where a line sits on screen, so even the fallback is far cheaper than measuring every line.
    private int NumberOf(MarkdownRichEditor editor, TextPointer line, ref int layoutQueries)
    {
        if (_anchorLine is not null && _anchorLine.IsInSameDocument(line))
        {
            var direction = line.CompareTo(_anchorLine);
            if (direction == 0)
            {
                return _anchorNumber;
            }

            var step = direction > 0 ? 1 : -1;
            var cursor = _anchorLine;
            var number = _anchorNumber;
            for (var walked = 0; walked < MaxAnchorWalk; walked++)
            {
                var next = cursor.GetLineStartPosition(step, out var moved);
                layoutQueries++;
                if (next is null || moved == 0)
                {
                    break;
                }

                cursor = next;
                number += step;
                if (cursor.CompareTo(line) == 0)
                {
                    return number;
                }
            }
        }

        return CountFromStart(editor, line, ref layoutQueries);
    }

    private static int CountFromStart(MarkdownRichEditor editor, TextPointer line, ref int layoutQueries)
    {
        var cursor = editor.Document.ContentStart.GetLineStartPosition(0);
        var number = 1;
        while (cursor is not null && cursor.CompareTo(line) < 0)
        {
            cursor = cursor.GetLineStartPosition(1, out var moved);
            layoutQueries++;
            if (moved == 0)
            {
                break;
            }

            number++;
        }

        return number;
    }

    private void AddLineNumber(int number, Rect rect, double fontSize, FontFamily fontFamily)
    {
        var label = new TextBlock
        {
            Text = number.ToString(CultureInfo.CurrentCulture),
            FontFamily = fontFamily,
            FontSize = fontSize,
            Foreground = LineNumberBrush,
            Width = NumberColumnWidth,
            TextAlignment = TextAlignment.Right,
            IsHitTestVisible = false,
        };
        SetLeft(label, 0d);
        // Centre the number vertically on the (possibly taller) heading/paragraph line.
        SetTop(label, rect.Top + Math.Max(0d, (rect.Height - fontSize * 1.3d) / 2d));
        Children.Add(label);
    }

    private void BuildFoldToggles(MarkdownRichEditor editor, double viewportHeight)
    {
        var stopwatch = ScrollProfiler.Start();
        var walked = 0;
        var drawn = 0;
        var rects = 0;

        // Begin at the Block the Viewport opens on rather than at the first Block of the document: a
        // Fold Toggle above the Viewport is not drawn, so probing for one is pure waste (INV-074).
        rects++;
        var start = TopLevelBlockAt(editor, editor.GetPositionFromPoint(new Point(0, 0), snapToText: true))
            ?? editor.Document.Blocks.FirstBlock;

        for (var block = start; block is not null; block = block.NextBlock)
        {
            walked++;
            if (!editor.IsSectionHeading(block))
            {
                continue;
            }

            rects++;
            var rect = block.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            if (rect == Rect.Empty || rect.Bottom < 0)
            {
                continue;
            }

            // Blocks are in document order, so once one falls below the viewport every later one does
            // too — stop walking rather than probe the rest of the document each scroll.
            if (rect.Top > viewportHeight)
            {
                break;
            }

            AddFoldToggle(editor, block, rect);
            drawn++;
        }

        ScrollProfiler.Record(stopwatch, "EditorGutter.BuildFoldToggles", walked, drawn, rects);
    }

    // The top-level Block a position sits in — climbing out of a List Item or Table Cell, because the
    // gutter walks the document's own Block sequence and a nested Paragraph is not part of it.
    private static Block? TopLevelBlockAt(MarkdownRichEditor editor, TextPointer? position)
    {
        if (position is null)
        {
            return null;
        }

        TextElement? element = position.Paragraph;
        Block? topLevel = position.Paragraph;
        while (element?.Parent is TextElement parent)
        {
            if (parent is Block block)
            {
                topLevel = block;
            }

            element = parent;
        }

        return topLevel?.Parent == editor.Document ? topLevel : null;
    }

    private void AddFoldToggle(MarkdownRichEditor editor, Block heading, Rect rect)
    {
        var folded = editor.IsFolded(heading);
        var chevron = new TextBlock
        {
            Text = folded ? CollapsedGlyph : ExpandedGlyph,
            FontSize = ChevronFontSize,
            Foreground = ChevronBrush,
            Width = ChevronColumnWidth,
            TextAlignment = TextAlignment.Center,
            Cursor = Cursors.Hand,
            ToolTip = folded ? "Expand section" : "Collapse section",
        };
        SetLeft(chevron, NumberColumnWidth + ChevronGap);
        // Centre the small chevron on the (possibly taller) heading line.
        SetTop(chevron, rect.Top + Math.Max(0d, (rect.Height - ChevronFontSize * 1.3d) / 2d));

        chevron.MouseEnter += (_, _) => chevron.Foreground = ChevronHoverBrush;
        chevron.MouseLeave += (_, _) => chevron.Foreground = ChevronBrush;
        chevron.MouseLeftButtonUp += (_, _) =>
        {
            editor.ToggleFold(heading);
            ScheduleRefresh();
        };

        Children.Add(chevron);
    }
}
