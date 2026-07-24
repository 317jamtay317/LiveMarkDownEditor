using System.Windows;
using System.Windows.Media;

namespace UI.Controls;

/// <summary>
/// Draws the Document Sheet itself in Page View: the paper the Visual Document is laid out on, and the
/// Page Break rule where each 8.5 × 11 Page ends and the next begins. It is drawn <em>behind</em> the
/// editing surface — which Page View makes transparent — so a Page Break passes under the text rather
/// than striking through it (INV-058).
/// </summary>
/// <remarks>
/// A bare <see cref="FrameworkElement"/> rather than a Control: it has no interaction and no template,
/// only a fill and a rule per Page boundary. Its brushes are resource references, so a theme swap
/// repaints the Sheet without touching the document. Size it to the editing surface — Page View grows
/// that in whole Pages (see <see cref="DocumentSheet"/>), so every boundary this draws is a Page's.
/// </remarks>
public sealed class DocumentSheetBackdrop : FrameworkElement
{
    private const double BreakThickness = 1d;

    /// <summary>Identifies the <see cref="SheetBrush"/> dependency property.</summary>
    public static readonly DependencyProperty SheetBrushProperty = DependencyProperty.Register(
        nameof(SheetBrush),
        typeof(Brush),
        typeof(DocumentSheetBackdrop),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Identifies the <see cref="PageBreakBrush"/> dependency property.</summary>
    public static readonly DependencyProperty PageBreakBrushProperty = DependencyProperty.Register(
        nameof(PageBreakBrush),
        typeof(Brush),
        typeof(DocumentSheetBackdrop),
        new FrameworkPropertyMetadata(Brushes.Gainsboro, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Identifies the <see cref="PageHeight"/> dependency property.</summary>
    public static readonly DependencyProperty PageHeightProperty = DependencyProperty.Register(
        nameof(PageHeight),
        typeof(double),
        typeof(DocumentSheetBackdrop),
        new FrameworkPropertyMetadata(DocumentSheet.PageHeight, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Initialises the Sheet, taking its paper and Page Break colours from the active palette.</summary>
    public DocumentSheetBackdrop()
    {
        IsHitTestVisible = false;
        SetResourceReference(SheetBrushProperty, "EditorBackgroundBrush");
        SetResourceReference(PageBreakBrushProperty, "PageBreakBrush");
    }

    /// <summary>The paper the Visual Document is laid out on — the Sheet's own fill.</summary>
    public Brush SheetBrush
    {
        get => (Brush)GetValue(SheetBrushProperty);
        set => SetValue(SheetBrushProperty, value);
    }

    /// <summary>The rule drawn where one Page ends and the next begins.</summary>
    public Brush PageBreakBrush
    {
        get => (Brush)GetValue(PageBreakBrushProperty);
        set => SetValue(PageBreakBrushProperty, value);
    }

    /// <summary>
    /// One Page's height, in device-independent units: where the Page Break rules fall. Bind it to the
    /// Page Setup's page height so the rules follow the Page Orientation — 1056 units upright, 816
    /// turned (INV-061).
    /// </summary>
    public double PageHeight
    {
        get => (double)GetValue(PageHeightProperty);
        set => SetValue(PageHeightProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0d || height <= 0d)
        {
            return;
        }

        drawingContext.DrawRectangle(SheetBrush, null, new Rect(0d, 0d, width, height));

        // Frozen when it can be — a palette brush merged live by the theme service is not always
        // freezable, and freezing a pen over one throws rather than merely declining.
        var pen = new Pen(PageBreakBrush, BreakThickness);
        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        // Every Page boundary except the Sheet's own bottom edge, which is already a page edge. A
        // page height that is not yet a real one (a binding still settling) draws no rules — a
        // non-positive step would otherwise never advance.
        var pageHeight = PageHeight;
        if (pageHeight <= 0d)
        {
            return;
        }

        for (var pageBottom = pageHeight; pageBottom < height; pageBottom += pageHeight)
        {
            // Half-pixel offset so a one-unit rule lands on a device pixel instead of blurring across two.
            var y = Math.Round(pageBottom) + (BreakThickness / 2d);
            drawingContext.DrawLine(pen, new Point(0d, y), new Point(width, y));
        }
    }
}
