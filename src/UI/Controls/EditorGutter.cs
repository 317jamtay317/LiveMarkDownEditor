using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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

    private bool _refreshQueued;

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

    private void OnEditorInvalidated(object? sender, EventArgs e) => ScheduleRefresh();

    // Scrolling settles the layout synchronously, so the glyphs can be repositioned immediately.
    private void OnEditorScrolled(object? sender, ScrollChangedEventArgs e) => Refresh();

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
        var fontSize = editor.FontSize > 0 ? editor.FontSize : 13d;
        var line = editor.Document.ContentStart.GetLineStartPosition(0);
        var number = 1;
        while (line is not null)
        {
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
                }
            }

            line = line.GetLineStartPosition(1, out var moved);
            if (moved == 0)
            {
                break;
            }

            number++;
        }
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
        foreach (var block in editor.Document.Blocks)
        {
            if (!editor.IsSectionHeading(block))
            {
                continue;
            }

            var rect = block.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            if (rect == Rect.Empty || rect.Bottom < 0 || rect.Top > viewportHeight)
            {
                continue;
            }

            AddFoldToggle(editor, block, rect);
        }
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
