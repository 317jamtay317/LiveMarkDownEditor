using System.Windows.Documents;
using Domain;
using WpfBlock = System.Windows.Documents.Block;

namespace UI.Wysiwyg;

/// <summary>How a Block of the Visual Document carries part of a Change Highlight.</summary>
public enum ChangeHighlightTargetKind
{
    /// <summary>The Block's source lines were added or altered — it is shaded.</summary>
    Changed,

    /// <summary>Content was deleted immediately above this Block — its top edge carries the seam.</summary>
    RemovedAbove,

    /// <summary>
    /// Content was deleted past the end of the document, so no Block follows the seam — the last
    /// Block's bottom edge carries it.
    /// </summary>
    RemovedBelow,
}

/// <summary>
/// One Block of a Visual Document that carries part of a Change Highlight, and how.
/// </summary>
/// <param name="Block">The Block the Change Highlight is drawn against.</param>
/// <param name="Kind">Whether the Block is shaded, or merely anchors a deletion seam.</param>
public sealed record ChangeHighlightTarget(WpfBlock Block, ChangeHighlightTargetKind Kind);

/// <summary>
/// Resolves the Changed Regions of a Reload Difference to the Blocks of the reloaded Visual Document
/// that carry the Change Highlight, so the overlay knows what to draw and where (INV-060).
/// </summary>
/// <remarks>
/// It is a pure, view-only projection: it reads the Source Line Range each Block records and the
/// Changed Regions it is handed, and changes neither. A Block the projection introduced rather than
/// read from source — the trailing paragraph after a Block Island — records no range and so is never
/// shaded; it can still anchor a seam only by being the last Block.
/// </remarks>
public static class ChangeHighlightScanner
{
    /// <summary>Finds the Blocks of <paramref name="document"/> that carry <paramref name="regions"/>.</summary>
    /// <param name="document">The reloaded Visual Document.</param>
    /// <param name="regions">The Changed Regions of the Reload Difference, in document order.</param>
    /// <returns>
    /// The targets in document order. A Block is shaded at most once however many Changed Regions
    /// touch it, and may both be shaded and anchor a seam.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is <see langword="null"/>.</exception>
    public static IReadOnlyList<ChangeHighlightTarget> Scan(
        FlowDocument document,
        IReadOnlyList<ChangedRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(document, nameof(document));
        ArgumentNullException.ThrowIfNull(regions, nameof(regions));

        var blocks = document.Blocks.ToList();
        if (blocks.Count == 0 || regions.Count == 0)
        {
            return [];
        }

        var seams = SeamAnchors(blocks, regions);

        var targets = new List<ChangeHighlightTarget>();
        for (var index = 0; index < blocks.Count; index++)
        {
            var range = SourceLines.GetRange(blocks[index]);
            if (range is not null && regions.Any(region =>
                    region.Kind == ChangedRegionKind.Changed && range.Intersects(region.StartLine, region.LineCount)))
            {
                targets.Add(new ChangeHighlightTarget(blocks[index], ChangeHighlightTargetKind.Changed));
            }

            if (seams.TryGetValue(index, out var seamKind))
            {
                targets.Add(new ChangeHighlightTarget(blocks[index], seamKind));
            }
        }

        return targets;
    }

    // Which Block anchors each Removed seam: the first Block starting at or after the seam's line
    // carries it above itself, and a seam past every Block falls below the last one. Several seams
    // landing on the same Block draw one mark — a seam is a position, not a quantity.
    private static Dictionary<int, ChangeHighlightTargetKind> SeamAnchors(
        List<WpfBlock> blocks,
        IReadOnlyList<ChangedRegion> regions)
    {
        var anchors = new Dictionary<int, ChangeHighlightTargetKind>();
        foreach (var seam in regions.Where(region => region.Kind == ChangedRegionKind.Removed))
        {
            var below = blocks.FindIndex(block => SourceLines.GetRange(block) is { } range && range.StartLine >= seam.StartLine);
            if (below >= 0)
            {
                anchors[below] = ChangeHighlightTargetKind.RemovedAbove;
            }
            else
            {
                anchors[blocks.Count - 1] = ChangeHighlightTargetKind.RemovedBelow;
            }
        }

        return anchors;
    }
}
