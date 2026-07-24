namespace UI.Core;

/// <summary>
/// Which way the US Letter Page turns: part of the Page Setup, obeyed by the Document Sheet, the
/// Print Preview, and the printout alike (INV-061).
/// </summary>
public enum PageOrientation
{
    /// <summary>The Page upright: 8.5 × 11 inches (816 × 1056 device-independent units). The default.</summary>
    Portrait,

    /// <summary>The Page turned on its side: 11 × 8.5 inches (1056 × 816 device-independent units).</summary>
    Landscape,
}
