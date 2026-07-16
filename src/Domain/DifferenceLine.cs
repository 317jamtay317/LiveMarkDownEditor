namespace Domain;

/// <summary>
/// One line of a Conflict Difference: its text together with the side (or sides) it appears on.
/// </summary>
/// <remarks>
/// Part of INV-021. The text excludes the line's terminator, so a CRLF/LF difference alone never
/// makes two lines differ. This is a value object: two instances with the same <see cref="Kind"/>
/// and <see cref="Text"/> are equal.
/// </remarks>
public sealed record DifferenceLine
{
    /// <summary>Which side (or both) this line is present on.</summary>
    public DifferenceLineKind Kind { get; }

    /// <summary>The line's text, without its terminator. Never <see langword="null"/>.</summary>
    public string Text { get; }

    /// <summary>Creates a <see cref="DifferenceLine"/> of the given kind and text.</summary>
    /// <param name="kind">Which side (or both) the line is present on.</param>
    /// <param name="text">The line's text without its terminator; the empty string for a blank line.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="text"/> is <see langword="null"/> (violates INV-021).
    /// </exception>
    public DifferenceLine(DifferenceLineKind kind, string text)
    {
        Kind = kind;
        Text = text ?? throw new ArgumentNullException(
            nameof(text),
            "A Difference Line's text is never null (INV-021).");
    }
}
