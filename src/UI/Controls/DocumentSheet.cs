using System.Windows;

namespace UI.Controls;

/// <summary>
/// The pure rule for the Document Sheet — the stack of fixed-size Pages the Visual Document is laid out
/// on in Page View. Fixing the width to one Page confines every element it holds, tables included, to
/// that width instead of letting it stretch to the pane, and sizing the Sheet in whole Pages makes it
/// look like paper: a full 8.5 × 11 Page even when the content is short, and a new Page the moment the
/// content outgrows the last one (INV-058).
/// </summary>
public static class DocumentSheet
{
    // Sub-pixel slack: a content height a rounding hair over a Page boundary must not add a whole empty
    // Page, so the overflow has to clear this much before the next Page is counted.
    private const double Tolerance = 0.5d;

    /// <summary>
    /// A Page's width, in device-independent units: the US Letter width of 8.5 inches at 96 units per
    /// inch (816). It is fixed, so the Visual Document's layout width does not track the editing surface
    /// — which is what confines a table to the page and stops a widen from reflowing the document
    /// (INV-058).
    /// </summary>
    public const double Width = 816d;

    /// <summary>
    /// A Page's height, in device-independent units: the US Letter height of 11 inches at 96 units per
    /// inch (1056). The Sheet is always a whole number of these tall (INV-058).
    /// </summary>
    public const double PageHeight = 1056d;

    /// <summary>
    /// The Document Sheet's page margins: the inset from the Sheet's edges to its content, the way a
    /// word processor leaves a margin around the page. Presentation-only (INV-058).
    /// </summary>
    public static Thickness PagePadding { get; } = new(left: 72d, top: 64d, right: 72d, bottom: 64d);

    /// <summary>
    /// The number of Pages the Sheet needs to hold content of the given height: one Page while the
    /// content fits on it, and a further Page for each overflow — so the Sheet gains its next Page as
    /// soon as the content needs it (INV-058).
    /// </summary>
    /// <param name="contentHeight">The Visual Document's laid-out height, in device-independent units.</param>
    /// <returns>The Page count — never fewer than one, even before the surface has been measured.</returns>
    public static int PageCount(double contentHeight)
    {
        if (double.IsNaN(contentHeight) || contentHeight <= PageHeight + Tolerance)
        {
            return 1;
        }

        return (int)Math.Ceiling((contentHeight - Tolerance) / PageHeight);
    }

    /// <summary>
    /// The Sheet's height for content of the given height: always a whole number of Pages (INV-058).
    /// </summary>
    /// <param name="contentHeight">The Visual Document's laid-out height, in device-independent units.</param>
    /// <returns>The Sheet height, in device-independent units.</returns>
    public static double HeightFor(double contentHeight) => PageCount(contentHeight) * PageHeight;

    /// <summary>
    /// The blank space that fills out the rest of the last Page: what the Sheet adds below content of
    /// the given height so it ends on a Page boundary rather than wherever the content happens to stop
    /// (INV-058).
    /// </summary>
    /// <param name="contentHeight">The Visual Document's laid-out height, in device-independent units.</param>
    /// <returns>The filler height, in device-independent units; never negative.</returns>
    public static double TrailingSpaceFor(double contentHeight) =>
        double.IsNaN(contentHeight)
            ? PageHeight
            : Math.Max(0d, HeightFor(contentHeight) - contentHeight);
}
