using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using UI.Core;

namespace UI.Controls;

/// <summary>
/// The Print Preview's pages: the given document paginated for real — by WPF's own
/// <see cref="DocumentPaginator"/>, the same pagination Print sends to the printer — into a vertical
/// stack of Pages at the Page Setup's oriented size and Print Margins (INV-061). Unlike the Document
/// Sheet, whose Page Breaks only mark boundaries, each page here holds exactly the content the printed
/// page would.
/// </summary>
/// <remarks>
/// A bare <see cref="FrameworkElement"/> hosting the paginator's page visuals directly (the
/// <see cref="DocumentSheetBackdrop"/> pattern): it has no interaction and no template — a paper
/// rectangle and a page visual per Page. The pages are drawn as print draws them — dark theme or
/// light, paper is paper — so the preview shows the printout, not the editor.
/// </remarks>
public sealed class PrintPreviewPages : FrameworkElement
{
    // The gap of canvas kept between two pages, and the paper's edge.
    private const double PageGap = 24d;
    private static readonly Pen EdgePen = CreateEdgePen();

    private readonly VisualCollection _visuals;

    /// <summary>Identifies the <see cref="Document"/> dependency property.</summary>
    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document),
        typeof(FlowDocument),
        typeof(PrintPreviewPages),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnPagesChanged));

    /// <summary>Identifies the <see cref="Setup"/> dependency property.</summary>
    public static readonly DependencyProperty SetupProperty = DependencyProperty.Register(
        nameof(Setup),
        typeof(PageSetup),
        typeof(PrintPreviewPages),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnPagesChanged));

    /// <summary>Creates the pages host.</summary>
    public PrintPreviewPages() => _visuals = new VisualCollection(this);

    /// <summary>The self-contained document to paginate and preview.</summary>
    public FlowDocument? Document
    {
        get => (FlowDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    /// <summary>The Page Setup the pages are laid out under; <see langword="null"/> means the default (INV-061).</summary>
    public PageSetup? Setup
    {
        get => (PageSetup?)GetValue(SetupProperty);
        set => SetValue(SetupProperty, value);
    }

    /// <inheritdoc />
    protected override int VisualChildrenCount => _visuals.Count;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => _visuals[index];

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var setup = Setup ?? PageSetup.Default;
        var pageCount = Math.Max(1, _visuals.Count);
        return new Size(
            setup.PageWidth,
            (pageCount * setup.PageHeight) + ((pageCount - 1) * PageGap));
    }

    private static void OnPagesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((PrintPreviewPages)d).Paginate();

    // Lays the document out into its printed pages and hosts one visual per Page: the paper, its
    // edge, and the paginator's own page visual — the very pages Print produces (INV-061).
    private void Paginate()
    {
        // Detach the page visuals from the old hosts first: the paginator caches its pages, so the
        // same page visual can come back from GetPage on the next pagination — and a Visual may only
        // ever have one parent.
        foreach (var visual in _visuals)
        {
            ((ContainerVisual)visual).Children.Clear();
        }

        _visuals.Clear();

        if (Document is not { } document)
        {
            return;
        }

        var setup = Setup ?? PageSetup.Default;
        document.ColumnWidth = double.PositiveInfinity;
        document.PageWidth = setup.PageWidth;
        document.PageHeight = setup.PageHeight;
        document.PagePadding = setup.Margins.ToThickness();

        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.ComputePageCount();

        for (var index = 0; index < paginator.PageCount; index++)
        {
            var page = paginator.GetPage(index);

            // Paper is paper whatever the theme: the preview shows the printout, not the editor.
            var paper = new DrawingVisual();
            using (var context = paper.RenderOpen())
            {
                context.DrawRectangle(
                    Brushes.White, EdgePen, new Rect(0d, 0d, setup.PageWidth, setup.PageHeight));
            }

            var host = new ContainerVisual
            {
                Offset = new Vector(0d, index * (setup.PageHeight + PageGap)),
            };
            host.Children.Add(paper);
            host.Children.Add(page.Visual);
            _visuals.Add(host);
        }

        InvalidateMeasure();
    }

    private static Pen CreateEdgePen()
    {
        var pen = new Pen(Brushes.Gray, 1d);
        pen.Freeze();
        return pen;
    }
}
