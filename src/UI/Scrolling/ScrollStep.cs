namespace UI.Scrolling;

/// <summary>
/// The arithmetic of scrolling by a Wheel Notch: how far one notch moves a Viewport (the Scroll
/// Step), where a notch leaves the Scroll Target, and how the Viewport settles onto that target
/// frame by frame (INV-075).
/// </summary>
/// <remarks>
/// Pure and free of WPF, so the rules a wheel gesture obeys are stated — and tested — without a
/// window. The behaviour that owns the gesture reads the surface's metrics and moves its Viewport;
/// every number it moves by comes from here.
/// <para>
/// A stock WPF <c>ScrollViewer</c> does neither of the two things this exists for: it discards the
/// wheel delta's magnitude and scrolls a fixed three lines of sixteen pixels however hard the wheel
/// is spun, and it jumps that distance in a single frame. Measuring the step in the surface's own
/// line height is what makes a notch move a consistent amount of reading, and honouring the delta is
/// what makes spinning faster travel further.
/// </para>
/// </remarks>
public static class ScrollStep
{
    /// <summary>One Wheel Notch, as Windows reports it in a wheel delta.</summary>
    public const double NotchDelta = 120d;

    // What "one screen at a time" means in practice: very nearly a Viewport, less an overlap so the
    // reader keeps a line or two of their place across the jump.
    private const double ScreenFraction = 0.9d;

    // Closer than this and the travel is over. Without a landing distance the Viewport only ever
    // closes a fraction of the gap, never arrives, and whatever drives the animation never stops.
    private const double LandingDistance = 0.5d;

    /// <summary>
    /// How far <paramref name="wheelDelta"/> moves a Viewport, as a signed distance: positive moves
    /// down the document, negative up.
    /// </summary>
    /// <param name="wheelDelta">
    /// The wheel delta as reported, where <see cref="NotchDelta"/> is one notch turned away from the
    /// user. A fast spin arrives as a multiple and travels proportionally further; a precision
    /// touchpad's fraction of a notch travels proportionally less.
    /// </param>
    /// <param name="linesPerNotch">
    /// How many lines the system is configured to scroll per notch. Non-positive means the system
    /// asks for a screen at a time, in which case one notch is a Viewport.
    /// </param>
    /// <param name="lineHeight">
    /// The surface's own line height, so a notch moves the same amount of reading whatever the font.
    /// Non-positive — a surface not yet laid out — falls back to a screen.
    /// </param>
    /// <param name="viewportHeight">The height of the Viewport, used when a notch means a screen.</param>
    /// <returns>The signed distance to move the Scroll Target by.</returns>
    public static double DistanceFor(double wheelDelta, int linesPerNotch, double lineHeight, double viewportHeight)
    {
        // A step of the surface's own lines, or a screen when the system asks for one — or when the
        // surface cannot yet say how tall a line is. `linesPerNotch` is negative for a screen
        // ("WHEEL_PAGESCROLL"), so multiplying by it unguarded would reverse the direction of travel.
        var step = linesPerNotch > 0 && lineHeight > 0d
            ? linesPerNotch * lineHeight
            : Math.Max(0d, viewportHeight) * ScreenFraction;

        // Negated because the wheel turning away from the user reports a positive delta and means
        // moving up the document, which is a decreasing offset.
        return -(wheelDelta / NotchDelta) * step;
    }

    /// <summary>
    /// Where a Scroll Target sits after moving <paramref name="distance"/>, kept within the surface's
    /// scrollable range.
    /// </summary>
    /// <param name="currentTarget">
    /// The Scroll Target as it stands — the target of a travel already under way, so notches arriving
    /// during a spin accumulate rather than restarting from where the Viewport happens to have got to.
    /// </param>
    /// <param name="distance">The signed distance to move by, from <see cref="DistanceFor"/>.</param>
    /// <param name="scrollableHeight">
    /// How far the surface can scroll. Non-positive — content shorter than the Viewport — pins the
    /// target to the top.
    /// </param>
    /// <returns>The new Scroll Target, within <c>[0, scrollableHeight]</c>.</returns>
    public static double TargetFor(double currentTarget, double distance, double scrollableHeight) =>
        Math.Clamp(currentTarget + distance, 0d, Math.Max(0d, scrollableHeight));

    /// <summary>
    /// Where the Viewport sits one frame further into its travel toward <paramref name="scrollTarget"/>.
    /// </summary>
    /// <param name="currentOffset">The Viewport's offset now.</param>
    /// <param name="scrollTarget">The Scroll Target it is travelling toward.</param>
    /// <param name="smoothing">
    /// The fraction of the remaining gap to close this frame, clamped to <c>[0, 1]</c>. Smaller is a
    /// longer, gentler travel; 1 arrives immediately.
    /// </param>
    /// <returns>
    /// The offset to move to, which is exactly <paramref name="scrollTarget"/> once the gap closes to
    /// under a pixel — so the travel always terminates rather than approaching forever.
    /// </returns>
    public static double Settle(double currentOffset, double scrollTarget, double smoothing)
    {
        var gap = scrollTarget - currentOffset;
        return Math.Abs(gap) < LandingDistance
            ? scrollTarget
            : currentOffset + (gap * Math.Clamp(smoothing, 0d, 1d));
    }
}
