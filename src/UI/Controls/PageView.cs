using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace UI.Controls;

/// <summary>
/// The Page View behaviour: it lays the WYSIWYG editing surface out on a <see cref="DocumentSheet"/> of
/// whole 8.5 × 11 Pages floating on a scrolling canvas, so every element — tables included — is confined
/// to one page width, the Sheet grows a Page as soon as the content needs it, and the caret is kept in
/// view by driving that canvas. Turning Page View off restores the plain, full-pane editing surface
/// (INV-058).
/// </summary>
/// <remarks>
/// Authored as an attached behaviour — the sanctioned home for view-interaction logic outside a
/// ViewModel — so the page-view concern lives here rather than swelling <see cref="MarkdownRichEditor"/>.
/// It is attached to the surface <see cref="Grid"/> that holds the Editor Gutter and the editor, and is
/// given the outer <see cref="ScrollViewer"/> (the canvas) and the editor to reconfigure between the two
/// modes. In Page View the editor stops scrolling itself and grows to its content's full height, so the
/// whole Sheet moves as one piece when the canvas scrolls.
/// </remarks>
public static class PageView
{
    // The gap kept between the caret and the viewport edge when scrolling the caret into view, and the
    // band of canvas kept above and below the Sheet so it reads as a page floating on a surface.
    private const double CaretMargin = 48d;
    private const double SheetBand = 28d;

    private static readonly ConditionalWeakTable<Grid, SurfaceState> States = new();

    /// <summary>Identifies the <c>IsEnabled</c> attached property: whether the surface is in Page View.</summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(PageView),
        new PropertyMetadata(false, OnConfigurationChanged));

    /// <summary>Identifies the <c>Editor</c> attached property: the editing surface Page View lays out.</summary>
    public static readonly DependencyProperty EditorProperty = DependencyProperty.RegisterAttached(
        "Editor",
        typeof(MarkdownRichEditor),
        typeof(PageView),
        new PropertyMetadata(null, OnEditorChanged));

    /// <summary>Identifies the <c>Canvas</c> attached property: the outer scroller the Sheet floats on.</summary>
    public static readonly DependencyProperty CanvasProperty = DependencyProperty.RegisterAttached(
        "Canvas",
        typeof(ScrollViewer),
        typeof(PageView),
        new PropertyMetadata(null, OnConfigurationChanged));

    /// <summary>
    /// Identifies the <c>Source</c> attached property: the Source Panel to keep Scroll-Synced with the
    /// page. In Page View the editor's scrolling moves onto the canvas, so the Source Panel is synced to
    /// the canvas instead of the editor (INV-015, INV-058).
    /// </summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
        "Source",
        typeof(TextBoxBase),
        typeof(PageView),
        new PropertyMetadata(null, OnConfigurationChanged));

    /// <summary>Sets whether <paramref name="surface"/> is in Page View.</summary>
    /// <param name="surface">The surface holding the Editor Gutter and the editor.</param>
    /// <param name="value"><see langword="true"/> to enter Page View; <see langword="false"/> to leave it.</param>
    public static void SetIsEnabled(DependencyObject surface, bool value) =>
        surface.SetValue(IsEnabledProperty, value);

    /// <summary>Gets whether <paramref name="surface"/> is in Page View.</summary>
    /// <param name="surface">The surface to query.</param>
    /// <returns><see langword="true"/> when the surface is in Page View.</returns>
    public static bool GetIsEnabled(DependencyObject surface) =>
        (bool)surface.GetValue(IsEnabledProperty);

    /// <summary>Sets the editor Page View lays out on <paramref name="surface"/>.</summary>
    /// <param name="surface">The surface holding the editor.</param>
    /// <param name="value">The editing surface to lay out as a Document Sheet.</param>
    public static void SetEditor(DependencyObject surface, MarkdownRichEditor? value) =>
        surface.SetValue(EditorProperty, value);

    /// <summary>Gets the editor Page View lays out on <paramref name="surface"/>.</summary>
    /// <param name="surface">The surface to query.</param>
    /// <returns>The editing surface, or <see langword="null"/> when none is set.</returns>
    public static MarkdownRichEditor? GetEditor(DependencyObject surface) =>
        (MarkdownRichEditor?)surface.GetValue(EditorProperty);

    /// <summary>Sets the canvas the Document Sheet floats on for <paramref name="surface"/>.</summary>
    /// <param name="surface">The surface whose Sheet floats on the canvas.</param>
    /// <param name="value">The outer scroller that scrolls the Sheet.</param>
    public static void SetCanvas(DependencyObject surface, ScrollViewer? value) =>
        surface.SetValue(CanvasProperty, value);

    /// <summary>Gets the canvas the Document Sheet floats on for <paramref name="surface"/>.</summary>
    /// <param name="surface">The surface to query.</param>
    /// <returns>The outer scroller, or <see langword="null"/> when none is set.</returns>
    public static ScrollViewer? GetCanvas(DependencyObject surface) =>
        (ScrollViewer?)surface.GetValue(CanvasProperty);

    /// <summary>Sets the Source Panel kept Scroll-Synced with the page for <paramref name="surface"/>.</summary>
    /// <param name="surface">The surface whose page is synced to the Source Panel.</param>
    /// <param name="value">The Source Panel text view.</param>
    public static void SetSource(DependencyObject surface, TextBoxBase? value) =>
        surface.SetValue(SourceProperty, value);

    /// <summary>Gets the Source Panel kept Scroll-Synced with the page for <paramref name="surface"/>.</summary>
    /// <param name="surface">The surface to query.</param>
    /// <returns>The Source Panel text view, or <see langword="null"/> when none is set.</returns>
    public static TextBoxBase? GetSource(DependencyObject surface) =>
        (TextBoxBase?)surface.GetValue(SourceProperty);

    private static void OnConfigurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        Apply(d);

    private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Grid surface)
        {
            return;
        }

        var state = States.GetValue(surface, _ => new SurfaceState());

        if (e.OldValue is MarkdownRichEditor oldEditor)
        {
            oldEditor.TextChanged -= state.OnCaretMoved;
            oldEditor.SelectionChanged -= state.OnCaretMoved;
            oldEditor.SizeChanged -= state.OnSheetResized;
        }

        if (e.NewValue is MarkdownRichEditor newEditor)
        {
            state.Bind(surface, newEditor);
            newEditor.TextChanged += state.OnCaretMoved;
            newEditor.SelectionChanged += state.OnCaretMoved;

            // The Sheet is sized in whole Pages, so every change to how tall the content lays out — a
            // typed line, a reload, a fold — is re-checked against the Page boundary.
            newEditor.SizeChanged += state.OnSheetResized;
        }

        Apply(surface);
    }

    private static void Apply(DependencyObject host)
    {
        if (host is not Grid surface || GetEditor(surface) is not { } editor)
        {
            return;
        }

        var state = States.GetValue(surface, _ => new SurfaceState());
        state.Bind(surface, editor);
        var canvas = GetCanvas(surface);

        if (GetIsEnabled(surface))
        {
            EnterPageView(surface, editor, canvas, state);
        }
        else
        {
            ExitPageView(surface, editor, canvas, state);
        }
    }

    private static void EnterPageView(Grid surface, MarkdownRichEditor editor, ScrollViewer? canvas, SurfaceState state)
    {
        // The editor stops scrolling itself and grows to the full height of its content.
        editor.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

        // A fixed-width page with word-processor margins and a visible edge...
        editor.Width = DocumentSheet.Width;
        editor.VerticalAlignment = VerticalAlignment.Top;
        editor.BorderThickness = new Thickness(1d);
        editor.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");

        // ...made a whole number of 8.5 x 11 Pages tall, so a short document still shows a full page and
        // a long one gains its next Page as soon as the content needs it. The Sheet is snapped to its
        // Pages once it has been laid out at the plain page margins, not from a stale height.
        state.ResetPageMargins();
        state.QueueSnapToWholePages();

        // The paper and the Page Break rules are drawn by the DocumentSheetBackdrop behind the editing
        // surface, so a break passes under the text; the surface itself goes transparent to let it show.
        editor.Background = Brushes.Transparent;

        // The surface hugs [gutter | Sheet] and is centred on the canvas with equal margins, a band of
        // canvas kept above and below it. Centring is native: the canvas does not scroll horizontally, so
        // it measures the surface at the viewport width and HorizontalAlignment=Center centres it —
        // reliable and free of any layout-timing recomputation.
        surface.HorizontalAlignment = HorizontalAlignment.Center;
        surface.Margin = new Thickness(0d, SheetBand, 0d, SheetBand);
        SetEditorColumnWidth(surface, editor, GridLength.Auto);

        if (canvas is not null)
        {
            canvas.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            canvas.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            canvas.SetResourceReference(Control.BackgroundProperty, "EditorCanvasBrush");

            // Reveal find matches and heading jumps by scrolling the canvas, not the editor's own (now
            // disabled) scroll.
            editor.RevealRectOverride = rect => Reveal(canvas, editor, rect);

            // The editor no longer scrolls itself, so the Source Panel is Scroll-Synced to the canvas.
            if (GetSource(surface) is { } source)
            {
                ScrollSync.SetSyncPartner(editor, null);
                ScrollSync.SetSyncPartner(canvas, source);
                ScrollSync.SetSyncPartner(source, canvas);
            }
        }
    }

    private static void ExitPageView(Grid surface, MarkdownRichEditor editor, ScrollViewer? canvas, SurfaceState state)
    {
        // The whole-Page filler goes with the Sheet.
        state.ForgetPageFiller();

        // Back to a plain surface that fills the pane, paints its own background, and scrolls itself.
        editor.ClearValue(TextBoxBase.VerticalScrollBarVisibilityProperty);
        editor.ClearValue(TextBoxBase.HorizontalScrollBarVisibilityProperty);
        editor.ClearValue(FrameworkElement.WidthProperty);
        editor.ClearValue(FrameworkElement.VerticalAlignmentProperty);
        editor.ClearValue(Control.PaddingProperty);
        editor.ClearValue(Control.BorderThicknessProperty);
        editor.ClearValue(Control.BorderBrushProperty);
        editor.ClearValue(Control.BackgroundProperty);
        editor.RevealRectOverride = null;

        surface.ClearValue(FrameworkElement.HorizontalAlignmentProperty);
        surface.ClearValue(FrameworkElement.MarginProperty);
        SetEditorColumnWidth(surface, editor, new GridLength(1d, GridUnitType.Star));

        if (canvas is not null)
        {
            canvas.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            canvas.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            canvas.ClearValue(Control.BackgroundProperty);

            // The editor scrolls itself again, so the Source Panel is Scroll-Synced back to the editor.
            if (GetSource(surface) is { } source)
            {
                ScrollSync.SetSyncPartner(canvas, null);
                ScrollSync.SetSyncPartner(editor, source);
                ScrollSync.SetSyncPartner(source, editor);
            }
        }
    }

    private static void SetEditorColumnWidth(Grid surface, UIElement editor, GridLength width)
    {
        var column = Grid.GetColumn(editor);
        if (column >= 0 && column < surface.ColumnDefinitions.Count)
        {
            surface.ColumnDefinitions[column].Width = width;
        }
    }

    private static void Reveal(ScrollViewer canvas, MarkdownRichEditor editor, Rect rect)
    {
        if (rect == Rect.Empty || !editor.IsDescendantOf(canvas))
        {
            return;
        }

        var top = editor.TransformToAncestor(canvas).Transform(new Point(rect.X, rect.Y)).Y;
        if (top < CaretMargin)
        {
            canvas.ScrollToVerticalOffset(canvas.VerticalOffset + top - CaretMargin);
        }
        else if (top + rect.Height > canvas.ViewportHeight - CaretMargin)
        {
            canvas.ScrollToVerticalOffset(
                canvas.VerticalOffset + (top + rect.Height) - canvas.ViewportHeight + CaretMargin);
        }
    }

    // Per-surface state: the caret-follow debounce, the Sheet's whole-Page filler, and the handlers
    // bound to this surface's editor and canvas, kept so they can be removed when Page View is turned
    // off or the editor is detached.
    private sealed class SurfaceState
    {
        private bool _caretFollowQueued;
        private bool _pageSnapQueued;
        private double _trailingSpace;
        private Grid? _surface;
        private MarkdownRichEditor? _editor;

        public void Bind(Grid surface, MarkdownRichEditor editor)
        {
            _surface = surface;
            _editor = editor;
        }

        // The Sheet grew or shrank — check whether the content still ends on a Page boundary.
        public void OnSheetResized(object? sender, EventArgs e) => QueueSnapToWholePages();

        // Puts the Sheet back to the plain page margins, with no whole-Page filler on them. Paired with
        // a queued snap, which measures the filler afresh once the Sheet has laid out at these margins.
        public void ResetPageMargins()
        {
            _trailingSpace = 0d;
            if (_editor is { } editor)
            {
                editor.Padding = DocumentSheet.PagePadding;
            }
        }

        // Snaps the Sheet to whole Pages after the current layout pass. Coalesced to one snap per
        // dispatcher cycle: setting the filler resizes the Sheet, which lands back here.
        public void QueueSnapToWholePages()
        {
            if (_pageSnapQueued || _surface is not { } surface || _editor is not { } editor || !GetIsEnabled(surface))
            {
                return;
            }

            _pageSnapQueued = true;
            editor.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                () =>
                {
                    _pageSnapQueued = false;
                    SnapToWholePages();
                });
        }

        // Fills out the rest of the last Page with blank Sheet, so the Sheet is always a whole number of
        // Pages tall and gains its next Page the moment the content outgrows the last one. The filler
        // rides on the Sheet's bottom page margin, so the Sheet's own height stays the measure of the
        // content — subtracting the filler back off gives the height the content actually laid out to.
        private void SnapToWholePages()
        {
            if (_surface is not { } surface || _editor is not { } editor || !GetIsEnabled(surface))
            {
                return;
            }

            var sheetHeight = editor.ActualHeight;
            if (sheetHeight <= 0d)
            {
                return;
            }

            var trailingSpace = DocumentSheet.TrailingSpaceFor(sheetHeight - _trailingSpace);
            if (Math.Abs(trailingSpace - _trailingSpace) < 0.5d)
            {
                return;
            }

            _trailingSpace = trailingSpace;
            var margins = DocumentSheet.PagePadding;
            editor.Padding = new Thickness(margins.Left, margins.Top, margins.Right, margins.Bottom + trailingSpace);
        }

        // Forgets the whole-Page filler; the caller clears the page margins it rode on with the rest of
        // the non-page surface.
        public void ForgetPageFiller() => _trailingSpace = 0d;

        // Coalesces caret moves (typing, navigation) to one canvas scroll per dispatcher cycle, and only
        // follows the caret while the user is actually editing this surface in Page View.
        public void OnCaretMoved(object? sender, EventArgs e)
        {
            if (_caretFollowQueued || _surface is not { } surface || _editor is not { } editor || !GetIsEnabled(surface))
            {
                return;
            }

            _caretFollowQueued = true;
            editor.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                () =>
                {
                    _caretFollowQueued = false;
                    if (!GetIsEnabled(surface) || !editor.IsKeyboardFocused || GetCanvas(surface) is not { } canvas)
                    {
                        return;
                    }

                    Reveal(canvas, editor, editor.CaretPosition.GetCharacterRect(LogicalDirection.Forward));
                });
        }
    }
}
