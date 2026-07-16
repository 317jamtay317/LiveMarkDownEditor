using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for <see cref="ConflictDifference"/>, the pure line-based comparison between the two sides
/// of a Conflict. Covers INV-021: computing a Conflict Difference is deterministic, accounts for
/// every line of both sides, and never mutates either side.
/// </summary>
public sealed class ConflictDifferenceTests
{
    private static IReadOnlyList<DifferenceLine> Compute(string session, string disk) =>
        ConflictDifference.Compute(new MarkdownSource(session), new MarkdownSource(disk));

    private static IEnumerable<string> TextsOfKind(
        IReadOnlyList<DifferenceLine> lines,
        params DifferenceLineKind[] kinds) =>
        lines.Where(line => kinds.Contains(line.Kind)).Select(line => line.Text);

    [Fact]
    public void Compute_GivenNullSession_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(() => ConflictDifference.Compute(null!, MarkdownSource.Empty));
    }

    [Fact]
    public void Compute_GivenNullDisk_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(() => ConflictDifference.Compute(MarkdownSource.Empty, null!));
    }

    [Fact]
    public void Compute_GivenEmptyTexts_YieldsNoLines()
    {
        Compute("", "").ShouldBeEmpty();
    }

    [Fact]
    public void Compute_GivenIdenticalTexts_YieldsAllUnchanged()
    {
        var lines = Compute("# Title\nBody", "# Title\nBody");

        lines.Select(line => line.Kind).ShouldAllBe(kind => kind == DifferenceLineKind.Unchanged);
        lines.Select(line => line.Text).ShouldBe(["# Title", "Body"]);
    }

    [Fact]
    public void Compute_GivenEmptySession_YieldsAllDiskOnly()
    {
        var lines = Compute("", "# Disk\nBody");

        lines.Select(line => line.Kind).ShouldAllBe(kind => kind == DifferenceLineKind.DiskOnly);
        lines.Select(line => line.Text).ShouldBe(["# Disk", "Body"]);
    }

    [Fact]
    public void Compute_GivenEmptyDisk_YieldsAllSessionOnly()
    {
        var lines = Compute("# Mine\nBody", "");

        lines.Select(line => line.Kind).ShouldAllBe(kind => kind == DifferenceLineKind.SessionOnly);
        lines.Select(line => line.Text).ShouldBe(["# Mine", "Body"]);
    }

    [Fact]
    public void Compute_GivenLineAddedOnDisk_YieldsDiskOnlyLine()
    {
        var lines = Compute("# Title\nBody", "# Title\nAdded\nBody");

        lines.ShouldBe([
            new DifferenceLine(DifferenceLineKind.Unchanged, "# Title"),
            new DifferenceLine(DifferenceLineKind.DiskOnly, "Added"),
            new DifferenceLine(DifferenceLineKind.Unchanged, "Body"),
        ]);
    }

    [Fact]
    public void Compute_GivenLineMissingOnDisk_YieldsSessionOnlyLine()
    {
        var lines = Compute("# Title\nDoomed\nBody", "# Title\nBody");

        lines.ShouldBe([
            new DifferenceLine(DifferenceLineKind.Unchanged, "# Title"),
            new DifferenceLine(DifferenceLineKind.SessionOnly, "Doomed"),
            new DifferenceLine(DifferenceLineKind.Unchanged, "Body"),
        ]);
    }

    [Fact]
    public void Compute_GivenReplacedLine_YieldsSessionOnlyThenDiskOnly()
    {
        var lines = Compute("# Title\nMine\nBody", "# Title\nTheirs\nBody");

        lines.ShouldBe([
            new DifferenceLine(DifferenceLineKind.Unchanged, "# Title"),
            new DifferenceLine(DifferenceLineKind.SessionOnly, "Mine"),
            new DifferenceLine(DifferenceLineKind.DiskOnly, "Theirs"),
            new DifferenceLine(DifferenceLineKind.Unchanged, "Body"),
        ]);
    }

    [Fact]
    public void Compute_PreservesCommonPrefixAndSuffixAsUnchanged()
    {
        var lines = Compute("a\nb\nMINE\ny\nz", "a\nb\nDISK\ny\nz");

        lines.ShouldBe([
            new DifferenceLine(DifferenceLineKind.Unchanged, "a"),
            new DifferenceLine(DifferenceLineKind.Unchanged, "b"),
            new DifferenceLine(DifferenceLineKind.SessionOnly, "MINE"),
            new DifferenceLine(DifferenceLineKind.DiskOnly, "DISK"),
            new DifferenceLine(DifferenceLineKind.Unchanged, "y"),
            new DifferenceLine(DifferenceLineKind.Unchanged, "z"),
        ]);
    }

    [Fact]
    public void Compute_GivenSameContentWithDifferentLineEndings_YieldsAllUnchanged()
    {
        var lines = Compute("a\r\nb", "a\nb");

        lines.Select(line => line.Kind).ShouldAllBe(kind => kind == DifferenceLineKind.Unchanged);
        lines.Select(line => line.Text).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Compute_GivenTrailingNewlineOnlyDifference_YieldsAllUnchanged()
    {
        var lines = Compute("a\nb\n", "a\nb");

        lines.Select(line => line.Kind).ShouldAllBe(kind => kind == DifferenceLineKind.Unchanged);
        lines.Select(line => line.Text).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Compute_GivenBlankInteriorLine_KeepsTheBlankLine()
    {
        Compute("a\n\nb", "a\n\nb").Select(line => line.Text).ShouldBe(["a", "", "b"]);
    }

    [Fact]
    public void Compute_GivenSameInputsTwice_YieldsIdenticalLines_INV021()
    {
        const string Session = "# Title\nMine\nShared\nGone";
        const string Disk = "# Title\nTheirs\nShared\nAdded";

        Compute(Session, Disk).ShouldBe(Compute(Session, Disk));
    }

    [Fact]
    public void Compute_AccountsForEveryLineOfBothSides_INV021()
    {
        const string Session = "# Title\nMine\nShared\nGone";
        const string Disk = "# Title\nTheirs\nShared\nAdded";

        var lines = Compute(Session, Disk);

        TextsOfKind(lines, DifferenceLineKind.Unchanged, DifferenceLineKind.SessionOnly)
            .ShouldBe(["# Title", "Mine", "Shared", "Gone"]);
        TextsOfKind(lines, DifferenceLineKind.Unchanged, DifferenceLineKind.DiskOnly)
            .ShouldBe(["# Title", "Theirs", "Shared", "Added"]);
    }
}
