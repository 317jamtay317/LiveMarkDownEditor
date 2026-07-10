using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace UI.Controls;

/// <summary>
/// The Source Gutter: the presentation-only margin strip drawn to the left of the Source Panel's
/// <see cref="TextBox"/>. It shows a Line Number for each visible source line of the Markdown
/// Document, mirroring the Source Panel's own layout and scroll position.
/// </summary>
/// <remarks>
/// Authored as a custom Control (this class plus a ResourceDictionary for its look), per the
/// project's Control exception to the zero-code-behind rule. The gutter never mutates the document:
/// it only reads the Source Panel's line layout and mirrors it, so it cannot change the Markdown
/// source (INV-014). Because the Source Panel does not wrap, each source line occupies one rendered
/// row, so a Line Number here is the 1-based ordinal of a source line. Glyph positions come from
/// <see cref="TextBox.GetRectFromCharacterIndex(int)"/>, which reports line positions relative to
/// the Source Panel's viewport; the gutter re-lays out whenever the Source Panel's text, size, or
/// scroll offset changes.
/// </remarks>
public sealed class SourceGutter : Canvas
{
    private const double NumberColumnWidth = 40d;
    private const double NumberRightPadding = 8d;

    /// <summary>Identifies the <see cref="Source"/> dependency property.</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(TextBox),
        typeof(SourceGutter),
        new PropertyMetadata(null, OnSourceChanged));

    /// <summary>Identifies the <see cref="LineNumberBrush"/> dependency property.</summary>
    public static readonly DependencyProperty LineNumberBrushProperty = DependencyProperty.Register(
        nameof(LineNumberBrush),
        typeof(Brush),
        typeof(SourceGutter),
        new PropertyMetadata(Brushes.Gray));

    private bool _refreshQueued;

    /// <summary>Initialises the gutter, refreshing once it is loaded into the visual tree.</summary>
    public SourceGutter()
    {
        Loaded += (_, _) => ScheduleRefresh();
    }

    /// <summary>The Source Panel <see cref="TextBox"/> whose source lines this gutter mirrors.</summary>
    public TextBox? Source
    {
        get => (TextBox?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>The brush used to paint Line Numbers.</summary>
    public Brush LineNumberBrush
    {
        get => (Brush)GetValue(LineNumberBrushProperty);
        set => SetValue(LineNumberBrushProperty, value);
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var gutter = (SourceGutter)d;
        if (e.OldValue is TextBox oldSource)
        {
            oldSource.TextChanged -= gutter.OnSourceInvalidated;
            oldSource.SizeChanged -= gutter.OnSourceInvalidated;
            oldSource.Loaded -= gutter.OnSourceInvalidated;
            oldSource.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(gutter.OnSourceScrolled));
        }

        if (e.NewValue is TextBox newSource)
        {
            newSource.TextChanged += gutter.OnSourceInvalidated;
            newSource.SizeChanged += gutter.OnSourceInvalidated;
            newSource.Loaded += gutter.OnSourceInvalidated;
            newSource.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(gutter.OnSourceScrolled));
        }

        gutter.ScheduleRefresh();
    }

    private void OnSourceInvalidated(object? sender, EventArgs e) => ScheduleRefresh();

    // A scroll fires a burst of ScrollChanged events; coalescing the gutter rebuild to a single
    // refresh per dispatcher cycle (rather than one per event) keeps scrolling smooth.
    private void OnSourceScrolled(object? sender, ScrollChangedEventArgs e) => ScheduleRefresh();

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

    // One Line Number per visible source line. The Source Panel does not wrap, so each source line
    // is a single rendered row and its display-line index equals its source-line ordinal. Only the
    // lines currently in the viewport are walked, positioned by their character rects.
    private void Refresh()
    {
        Children.Clear();

        var source = Source;
        if (source is null || source.ActualHeight <= 0)
        {
            return;
        }

        var first = source.GetFirstVisibleLineIndex();
        var last = source.GetLastVisibleLineIndex();
        if (first < 0 || last < first)
        {
            return;
        }

        var fontSize = source.FontSize > 0 ? source.FontSize : 13d;
        for (var lineIndex = first; lineIndex <= last; lineIndex++)
        {
            var charIndex = source.GetCharacterIndexFromLineIndex(lineIndex);
            var rect = source.GetRectFromCharacterIndex(charIndex);
            if (rect == Rect.Empty)
            {
                continue;
            }

            AddLineNumber(lineIndex + 1, rect, fontSize, source.FontFamily);
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
            Width = NumberColumnWidth - NumberRightPadding,
            TextAlignment = TextAlignment.Right,
            IsHitTestVisible = false,
        };
        SetLeft(label, 0d);
        SetTop(label, rect.Top);
        Children.Add(label);
    }
}
