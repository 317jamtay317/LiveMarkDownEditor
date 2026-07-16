namespace Domain;

/// <summary>
/// Where a <see cref="DifferenceLine"/> lives across the two sides of a Conflict.
/// </summary>
/// <remarks>
/// The kinds name which side a line is present on — never which side should win. Choosing a side is
/// the user's explicit decision (INV-006); how a kind is styled is presentation.
/// </remarks>
public enum DifferenceLineKind
{
    /// <summary>The line is present on both sides. Shown as context.</summary>
    Unchanged,

    /// <summary>The line is present only in the Editor Session's unsaved source text.</summary>
    SessionOnly,

    /// <summary>The line is present only in the Watched File's on-disk contents.</summary>
    DiskOnly,
}
