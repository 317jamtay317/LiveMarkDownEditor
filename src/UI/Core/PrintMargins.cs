using System.Windows;
using UI.Controls;

namespace UI.Core;

/// <summary>
/// The Print Margins: the inset from a Page's edges to its content — the page margins of the Document
/// Sheet on screen and of the printed page alike, one set of margins for every surface (INV-061).
/// Cannot be constructed invalid: each margin is non-negative, and the four together always leave a
/// positive writable area on the US Letter Page in either Page Orientation, so switching the
/// orientation can never invalidate them.
/// </summary>
public sealed record PrintMargins
{
    // One inch, in device-independent units.
    private const double Inch = 96d;

    /// <summary>
    /// Creates Print Margins, guarding the writable area (INV-061): no margin may be negative, and the
    /// two margins of each axis must leave room to write on the Page's shorter side (816 units), so the
    /// margins stay valid whichever way the Page Orientation turns the Page.
    /// </summary>
    /// <param name="left">The left margin, in device-independent units.</param>
    /// <param name="top">The top margin, in device-independent units.</param>
    /// <param name="right">The right margin, in device-independent units.</param>
    /// <param name="bottom">The bottom margin, in device-independent units.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a margin is negative, or when a pair of opposing margins leaves no writable area.
    /// </exception>
    public PrintMargins(double left, double top, double right, double bottom)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(left);
        ArgumentOutOfRangeException.ThrowIfNegative(top);
        ArgumentOutOfRangeException.ThrowIfNegative(right);
        ArgumentOutOfRangeException.ThrowIfNegative(bottom);

        // The Page's shorter side: its width upright, its height turned. Guarding both axes against it
        // keeps the margins writable in either orientation.
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(left + right, DocumentSheet.Width, nameof(right));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(top + bottom, DocumentSheet.Width, nameof(bottom));

        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>The left margin, in device-independent units.</summary>
    public double Left { get; }

    /// <summary>The top margin, in device-independent units.</summary>
    public double Top { get; }

    /// <summary>The right margin, in device-independent units.</summary>
    public double Right { get; }

    /// <summary>The bottom margin, in device-independent units.</summary>
    public double Bottom { get; }

    /// <summary>The Print Margins a named Margin Preset stands for (INV-061).</summary>
    /// <param name="preset">The preset to resolve. Must be one of the four named presets.</param>
    /// <returns>That preset's margins.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown for <see cref="MarginPreset.Custom"/> — Custom is the user's own values, not a preset
    /// with fixed ones — and for any undefined value.
    /// </exception>
    public static PrintMargins For(MarginPreset preset) => preset switch
    {
        MarginPreset.Normal => new PrintMargins(Inch, Inch, Inch, Inch),
        MarginPreset.Narrow => new PrintMargins(Inch / 2d, Inch / 2d, Inch / 2d, Inch / 2d),
        MarginPreset.Moderate => new PrintMargins(Inch * 0.75d, Inch, Inch * 0.75d, Inch),
        MarginPreset.Wide => new PrintMargins(Inch * 2d, Inch, Inch * 2d, Inch),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Custom has no fixed margins."),
    };

    /// <summary>
    /// The Margin Preset the given margins stand for: the named preset they match exactly, or
    /// <see cref="MarginPreset.Custom"/> when they match none — which is what makes the preset
    /// derivable from the margins rather than a second thing to persist (INV-061).
    /// </summary>
    /// <param name="margins">The margins to recognise.</param>
    /// <returns>The matching named preset, or <see cref="MarginPreset.Custom"/>.</returns>
    public static MarginPreset PresetOf(PrintMargins margins)
    {
        ArgumentNullException.ThrowIfNull(margins);

        if (margins == For(MarginPreset.Normal))
        {
            return MarginPreset.Normal;
        }

        if (margins == For(MarginPreset.Narrow))
        {
            return MarginPreset.Narrow;
        }

        if (margins == For(MarginPreset.Moderate))
        {
            return MarginPreset.Moderate;
        }

        if (margins == For(MarginPreset.Wide))
        {
            return MarginPreset.Wide;
        }

        return MarginPreset.Custom;
    }

    /// <summary>These margins as a WPF <see cref="Thickness"/> — the Sheet's padding, the printed page's padding.</summary>
    /// <returns>The margins in left, top, right, bottom order.</returns>
    public Thickness ToThickness() => new(Left, Top, Right, Bottom);
}
