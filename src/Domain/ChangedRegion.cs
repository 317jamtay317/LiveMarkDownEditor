namespace Domain;

/// <summary>What an External Change did to the lines a <see cref="ChangedRegion"/> covers.</summary>
public enum ChangedRegionKind
{
    /// <summary>
    /// The region's lines were added or altered, so they exist in the reloaded Markdown Document
    /// and can be shown.
    /// </summary>
    Changed,

    /// <summary>
    /// Lines were deleted, so nothing of them remains to show. The region is the empty seam they
    /// left behind, between the lines that closed over them.
    /// </summary>
    Removed,
}

/// <summary>
/// One contiguous run of a reloaded Markdown Document that a Reload Difference attributes to the
/// External Change — the unit the Change Highlight shades (INV-060).
/// </summary>
/// <remarks>
/// Line numbers are 0-based positions in the <em>reloaded</em> text, so a Changed region covers the
/// lines <c>[StartLine, StartLine + LineCount)</c>. A Removed region has a <see cref="LineCount"/>
/// of zero: it names the seam where deleted lines were, which is a position rather than a range.
/// This is a value object: two regions with the same kind and extent are equal.
/// </remarks>
/// <param name="Kind">What the External Change did to this run.</param>
/// <param name="StartLine">The 0-based line in the reloaded text where the run begins.</param>
/// <param name="LineCount">How many reloaded lines the run covers; zero for a Removed seam.</param>
public sealed record ChangedRegion(ChangedRegionKind Kind, int StartLine, int LineCount);
