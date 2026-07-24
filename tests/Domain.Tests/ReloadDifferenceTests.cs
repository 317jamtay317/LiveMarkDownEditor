using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for <see cref="ReloadDifference"/>, the pure comparison between the Markdown Document a
/// live reload replaced and the contents that replaced it. Covers INV-060: the Changed Regions are
/// exactly the content the External Change touched, expressed in the reloaded text's own line
/// numbers, computed deterministically and mutating neither side.
/// </summary>
public sealed class ReloadDifferenceTests
{
    private static IReadOnlyList<ChangedRegion> Compute(string before, string after) =>
        ReloadDifference.Compute(new MarkdownSource(before), new MarkdownSource(after));

    [Fact]
    public void Compute_GivenNullBefore_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(() => ReloadDifference.Compute(null!, MarkdownSource.Empty));
    }

    [Fact]
    public void Compute_GivenNullAfter_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(() => ReloadDifference.Compute(MarkdownSource.Empty, null!));
    }

    [Fact]
    public void Compute_GivenIdenticalTexts_YieldsNoChangedRegions_INV060()
    {
        Compute("# Title\nBody", "# Title\nBody").ShouldBeEmpty();
    }

    [Fact]
    public void Compute_GivenEmptyTexts_YieldsNoChangedRegions_INV060()
    {
        Compute("", "").ShouldBeEmpty();
    }

    [Fact]
    public void Compute_GivenAnAlteredLine_MarksThatLineChanged_INV060()
    {
        var regions = Compute("# Title\nBody\nTail", "# Title\nChanged\nTail");

        regions.ShouldHaveSingleItem().ShouldBe(new ChangedRegion(ChangedRegionKind.Changed, 1, 1));
    }

    [Fact]
    public void Compute_GivenAppendedLines_MarksThemChanged_INV060()
    {
        var regions = Compute("a\nb", "a\nb\nc\nd");

        regions.ShouldHaveSingleItem().ShouldBe(new ChangedRegion(ChangedRegionKind.Changed, 2, 2));
    }

    [Fact]
    public void Compute_GivenDeletedLines_MarksTheSeamRemovedAndShadesNothing_INV060()
    {
        var regions = Compute("a\nb\nc", "a\nc");

        // Nothing of "b" survives to shade, so the region is the empty seam where it was — the
        // position in the reloaded text of the line that closed over it.
        regions.ShouldHaveSingleItem().ShouldBe(new ChangedRegion(ChangedRegionKind.Removed, 1, 0));
    }

    [Fact]
    public void Compute_GivenLinesDeletedFromTheEnd_MarksTheSeamPastTheLastLine_INV060()
    {
        var regions = Compute("a\nb\nc", "a");

        regions.ShouldHaveSingleItem().ShouldBe(new ChangedRegion(ChangedRegionKind.Removed, 1, 0));
    }

    [Fact]
    public void Compute_GivenAReplacedRun_MarksOnlyTheNewLines_INV060()
    {
        // Two lines became one. That is one Changed region over the surviving line — not a Changed
        // region plus a Removed seam, which would mark the same edit twice.
        var regions = Compute("a\nold one\nold two\nz", "a\nnew\nz");

        regions.ShouldHaveSingleItem().ShouldBe(new ChangedRegion(ChangedRegionKind.Changed, 1, 1));
    }

    [Fact]
    public void Compute_GivenSeveralChanges_YieldsThemInOrderAndNonOverlapping_INV060()
    {
        var regions = Compute("a\nb\nc\nd\ne", "a\nB\nc\nD\ne");

        regions.ShouldBe([
            new ChangedRegion(ChangedRegionKind.Changed, 1, 1),
            new ChangedRegion(ChangedRegionKind.Changed, 3, 1),
        ]);
    }

    [Fact]
    public void Compute_GivenAnEmptyDocumentFilled_MarksEveryLineChanged_INV060()
    {
        var regions = Compute("", "a\nb");

        regions.ShouldHaveSingleItem().ShouldBe(new ChangedRegion(ChangedRegionKind.Changed, 0, 2));
    }

    [Fact]
    public void Compute_GivenOnlyALineTerminatorDifference_YieldsNoChangedRegions_INV060()
    {
        Compute("a\nb", "a\r\nb\r\n").ShouldBeEmpty();
    }

    [Fact]
    public void Compute_IsDeterministicAndMutatesNeitherSide_INV060()
    {
        var before = new MarkdownSource("a\nb\nc");
        var after = new MarkdownSource("a\nB\nc");

        var first = ReloadDifference.Compute(before, after);
        var second = ReloadDifference.Compute(before, after);

        second.ShouldBe(first);
        before.Text.ShouldBe("a\nb\nc");
        after.Text.ShouldBe("a\nB\nc");
    }

    [Fact]
    public void Compute_GivenChangedRegions_NumbersThemWithinTheReloadedText_INV060()
    {
        var after = "a\nB\nc\nD\ne";
        var regions = Compute("a\nb\nc\nd\ne", after);

        var lineCount = after.Split('\n').Length;
        regions.ShouldAllBe(region => region.StartLine >= 0 && region.StartLine + region.LineCount <= lineCount);
    }
}
