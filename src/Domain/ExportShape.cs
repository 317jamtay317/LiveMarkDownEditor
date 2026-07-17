namespace Domain;

/// <summary>
/// The Export Shape — which of two forms an Export as HTML writes. Both carry the same
/// <see cref="RenderedOutput"/> and differ only in what surrounds it (INV-032).
/// </summary>
public enum ExportShape
{
    /// <summary>
    /// A Standalone Page: the Rendered Output wrapped in a complete HTML document — a title and an
    /// embedded stylesheet — so the file stands on its own when opened in a browser.
    /// </summary>
    StandalonePage,

    /// <summary>
    /// An HTML Fragment: the Rendered Output alone, for pasting into a page that supplies its own
    /// surroundings.
    /// </summary>
    HtmlFragment,
}
