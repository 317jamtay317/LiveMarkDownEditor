namespace UI.Scrolling;

/// <summary>
/// The pure mapping behind Scroll Sync: given how far one view is scrolled, computes how far its
/// partner view must scroll to sit at the same fraction of its own scrollable height, so the two
/// views stay aligned top-to-top and bottom-to-bottom (INV-015).
/// </summary>
/// <remarks>
/// A view's <em>scrollable height</em> is its extent height minus its viewport height — the maximum
/// vertical offset it can reach. When a view's content fits (a zero scrollable height) there is no
/// meaningful fraction, so the mapping yields zero rather than dividing by zero. This is a pure
/// function of the three measurements; it holds no state and touches no document.
/// </remarks>
public static class ProportionalScroll
{
    /// <summary>
    /// Maps <paramref name="sourceOffset"/> to the partner view's vertical offset at the same
    /// fraction of scrollable height.
    /// </summary>
    /// <param name="sourceOffset">The scrolled view's current vertical offset.</param>
    /// <param name="sourceScrollableHeight">The scrolled view's scrollable height (extent − viewport).</param>
    /// <param name="targetScrollableHeight">The partner view's scrollable height (extent − viewport).</param>
    /// <returns>
    /// The partner's target vertical offset, in <c>[0, targetScrollableHeight]</c>. Zero when either
    /// view has no scrollable height.
    /// </returns>
    public static double TargetOffset(
        double sourceOffset,
        double sourceScrollableHeight,
        double targetScrollableHeight)
    {
        if (sourceScrollableHeight <= 0d || targetScrollableHeight <= 0d)
        {
            return 0d;
        }

        var fraction = Math.Clamp(sourceOffset / sourceScrollableHeight, 0d, 1d);
        return fraction * targetScrollableHeight;
    }
}
