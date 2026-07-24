using UI.Controls;

namespace UI.Core;

/// <summary>
/// The Page Setup: the single, editor-wide configuration of the Page — a <see cref="PageOrientation"/>
/// together with <see cref="PrintMargins"/>. Every paged surface obeys the one Page Setup alike: the
/// Document Sheet in Page View, the Print Preview's pages, and the printed page — so what the user sees
/// on screen is the page that prints (INV-061). It is presentation-and-output only: changing it never
/// changes a Markdown Document or the result of a Capture.
/// </summary>
public sealed record PageSetup
{
    /// <summary>Creates a Page Setup from an orientation and margins.</summary>
    /// <param name="orientation">Which way the US Letter Page turns.</param>
    /// <param name="margins">The Print Margins insetting the Page's content.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined orientation.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="margins"/> is null.</exception>
    public PageSetup(PageOrientation orientation, PrintMargins margins)
    {
        if (orientation is not (PageOrientation.Portrait or PageOrientation.Landscape))
        {
            throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "Only Portrait and Landscape exist.");
        }

        Orientation = orientation;
        Margins = margins ?? throw new ArgumentNullException(nameof(margins));
    }

    /// <summary>The default Page Setup: Portrait with Normal margins (INV-061).</summary>
    public static PageSetup Default { get; } = new(PageOrientation.Portrait, PrintMargins.For(MarginPreset.Normal));

    /// <summary>Which way the US Letter Page turns.</summary>
    public PageOrientation Orientation { get; }

    /// <summary>The Print Margins insetting the Page's content, on screen and in print alike.</summary>
    public PrintMargins Margins { get; }

    /// <summary>
    /// The Page's width in device-independent units: the US Letter width upright (816), its height
    /// turned (1056). The Document Sheet's fixed width (INV-058).
    /// </summary>
    public double PageWidth =>
        Orientation == PageOrientation.Portrait ? DocumentSheet.Width : DocumentSheet.PageHeight;

    /// <summary>
    /// The Page's height in device-independent units: the US Letter height upright (1056), its width
    /// turned (816). The page height the whole-Page arithmetic counts by (INV-058).
    /// </summary>
    public double PageHeight =>
        Orientation == PageOrientation.Portrait ? DocumentSheet.PageHeight : DocumentSheet.Width;
}
