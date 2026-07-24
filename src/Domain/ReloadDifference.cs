namespace Domain;

/// <summary>
/// Computes the Reload Difference: what an External Change did to the Markdown Document when it
/// reloaded live — the Markdown Document as the Editor Session held it, against the on-disk
/// contents that replaced it — as the Changed Regions the Change Highlight shades.
/// </summary>
/// <remarks>
/// Enforces INV-060. <see cref="Compute"/> is pure and deterministic — it holds no state, performs
/// no I/O, and never mutates either side. It is the reload counterpart of
/// <see cref="ConflictDifference"/> and is built on it: where a Conflict Difference accounts for
/// every line of both sides so the user can judge them, a Reload Difference keeps only what changed
/// and numbers it within the reloaded text, because that is the text now on screen.
/// </remarks>
public static class ReloadDifference
{
    /// <summary>
    /// Computes the Changed Regions between the Markdown Document a live reload replaced and the
    /// contents that replaced it.
    /// </summary>
    /// <param name="before">The Markdown Document's source text as the Editor Session held it.</param>
    /// <param name="after">The on-disk contents that replaced it.</param>
    /// <returns>
    /// The Changed Regions in document order, never overlapping, with line numbers into
    /// <paramref name="after"/>. Empty when the two sides say the same thing.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when either side is <see langword="null"/> (violates INV-060).
    /// </exception>
    public static IReadOnlyList<ChangedRegion> Compute(MarkdownSource before, MarkdownSource after)
    {
        ArgumentNullException.ThrowIfNull(before, nameof(before));
        ArgumentNullException.ThrowIfNull(after, nameof(after));

        var lines = ConflictDifference.Compute(before, after);
        var regions = new List<ChangedRegion>();

        // Walk the Difference Lines counting position in the reloaded text: an Unchanged or an
        // "after" line advances it, a line that exists only on the outgoing side does not. Each run
        // of non-Unchanged lines becomes one region, so a replaced run is marked once rather than
        // as a deletion and an insertion side by side.
        var line = 0;
        var index = 0;
        while (index < lines.Count)
        {
            if (lines[index].Kind == DifferenceLineKind.Unchanged)
            {
                line++;
                index++;
                continue;
            }

            var start = line;
            var added = 0;
            while (index < lines.Count && lines[index].Kind != DifferenceLineKind.Unchanged)
            {
                if (lines[index].Kind == DifferenceLineKind.DiskOnly)
                {
                    added++;
                    line++;
                }

                index++;
            }

            // A run that brought no line with it deleted outright: there is nothing to shade, so the
            // region is the empty seam the deleted lines left at this position.
            regions.Add(added > 0
                ? new ChangedRegion(ChangedRegionKind.Changed, start, added)
                : new ChangedRegion(ChangedRegionKind.Removed, start, 0));
        }

        return regions;
    }
}
