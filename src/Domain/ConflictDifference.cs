namespace Domain;

/// <summary>
/// Computes the Conflict Difference between the two sides of a Conflict: the Editor Session's
/// unsaved source text and the conflicting on-disk contents of the Watched File.
/// </summary>
/// <remarks>
/// Enforces INV-021. <see cref="Compute"/> is pure and deterministic — it holds no state, performs
/// no I/O, and never mutates either side. It accounts for every line of both sides: the Unchanged
/// and Session Only lines reproduce the session's lines in order, and the Unchanged and Disk Only
/// lines reproduce the disk's lines in order.
/// </remarks>
public static class ConflictDifference
{
    /// <summary>
    /// Computes the Conflict Difference between the Editor Session's unsaved source text and the
    /// Watched File's on-disk contents, as an ordered list of Difference Lines.
    /// </summary>
    /// <param name="session">The Editor Session's unsaved Markdown Document source text.</param>
    /// <param name="disk">The conflicting on-disk contents of the Watched File.</param>
    /// <returns>
    /// The Difference Lines in document order. Within a replaced run, Session Only lines precede
    /// Disk Only lines. Empty when both sides are empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when either side is <see langword="null"/> (violates INV-021).
    /// </exception>
    public static IReadOnlyList<DifferenceLine> Compute(MarkdownSource session, MarkdownSource disk)
    {
        ArgumentNullException.ThrowIfNull(session, nameof(session));
        ArgumentNullException.ThrowIfNull(disk, nameof(disk));

        var sessionLines = SplitLines(session.Text);
        var diskLines = SplitLines(disk.Text);

        var prefix = CommonPrefixLength(sessionLines, diskLines);
        var suffix = CommonSuffixLength(sessionLines, diskLines, prefix);

        var lines = new List<DifferenceLine>();
        AddRange(lines, sessionLines, 0, prefix, DifferenceLineKind.Unchanged);
        AddMiddle(lines, sessionLines[prefix..^suffix], diskLines[prefix..^suffix]);
        AddRange(lines, sessionLines, sessionLines.Length - suffix, suffix, DifferenceLineKind.Unchanged);
        return lines;
    }

    /// <summary>
    /// Splits text into its lines, excluding terminators. A trailing terminator ends the last line
    /// rather than starting a phantom empty one, so "a\nb\n" and "a\nb" split alike; interior blank
    /// lines survive. Both CRLF and LF are recognised.
    /// </summary>
    private static string[] SplitLines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd('\r');
        }

        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    private static int CommonPrefixLength(string[] session, string[] disk)
    {
        var max = Math.Min(session.Length, disk.Length);
        var length = 0;
        while (length < max && session[length] == disk[length])
        {
            length++;
        }

        return length;
    }

    private static int CommonSuffixLength(string[] session, string[] disk, int prefix)
    {
        var max = Math.Min(session.Length, disk.Length) - prefix;
        var length = 0;
        while (length < max && session[^(length + 1)] == disk[^(length + 1)])
        {
            length++;
        }

        return length;
    }

    /// <summary>
    /// Emits the differing middle — the two sides once their common prefix and suffix are removed —
    /// by walking a longest-common-subsequence table over their lines.
    /// </summary>
    private static void AddMiddle(List<DifferenceLine> lines, string[] session, string[] disk)
    {
        if (session.Length == 0 || disk.Length == 0)
        {
            AddRange(lines, session, 0, session.Length, DifferenceLineKind.SessionOnly);
            AddRange(lines, disk, 0, disk.Length, DifferenceLineKind.DiskOnly);
            return;
        }

        var lengths = LongestCommonSubsequenceLengths(session, disk);

        int s = 0, d = 0;
        while (s < session.Length && d < disk.Length)
        {
            if (session[s] == disk[d])
            {
                lines.Add(new DifferenceLine(DifferenceLineKind.Unchanged, session[s]));
                s++;
                d++;
            }
            else if (lengths[s + 1, d] >= lengths[s, d + 1])
            {
                // Prefer the session's side on a tie so a replaced run reads removed-then-added.
                lines.Add(new DifferenceLine(DifferenceLineKind.SessionOnly, session[s]));
                s++;
            }
            else
            {
                lines.Add(new DifferenceLine(DifferenceLineKind.DiskOnly, disk[d]));
                d++;
            }
        }

        AddRange(lines, session, s, session.Length - s, DifferenceLineKind.SessionOnly);
        AddRange(lines, disk, d, disk.Length - d, DifferenceLineKind.DiskOnly);
    }

    /// <summary>
    /// Builds the suffix-LCS table: <c>lengths[i, j]</c> is the length of the longest common
    /// subsequence of <c>session[i..]</c> and <c>disk[j..]</c>.
    /// </summary>
    private static int[,] LongestCommonSubsequenceLengths(string[] session, string[] disk)
    {
        var lengths = new int[session.Length + 1, disk.Length + 1];
        for (var s = session.Length - 1; s >= 0; s--)
        {
            for (var d = disk.Length - 1; d >= 0; d--)
            {
                lengths[s, d] = session[s] == disk[d]
                    ? lengths[s + 1, d + 1] + 1
                    : Math.Max(lengths[s + 1, d], lengths[s, d + 1]);
            }
        }

        return lengths;
    }

    private static void AddRange(
        List<DifferenceLine> lines,
        string[] source,
        int start,
        int count,
        DifferenceLineKind kind)
    {
        for (var i = start; i < start + count; i++)
        {
            lines.Add(new DifferenceLine(kind, source[i]));
        }
    }
}
