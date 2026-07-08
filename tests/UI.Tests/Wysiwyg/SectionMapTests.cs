using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for <see cref="SectionMap"/>, the pure computation of a Section Body: the contiguous run of
/// blocks a Section Heading owns (up to the next heading of equal or higher level). Underpins
/// INV-011 — folding hides exactly a Section Body and nothing more. Block levels are modelled as a
/// list where each entry is a heading level (1–6) or <see langword="null"/> for a non-heading block.
/// </summary>
public sealed class SectionMapTests
{
    [Fact]
    public void FindBody_HeadingFollowedByParagraphs_OwnsThoseParagraphs()
    {
        int?[] levels = [1, null, null];

        var body = SectionMap.FindBody(levels, headingIndex: 0);

        body.Start.ShouldBe(1);
        body.Count.ShouldBe(2);
    }

    [Fact]
    public void FindBody_StopsBeforeHeadingOfEqualLevel()
    {
        int?[] levels = [2, null, 2, null];

        var body = SectionMap.FindBody(levels, headingIndex: 0);

        body.Start.ShouldBe(1);
        body.Count.ShouldBe(1);
    }

    [Fact]
    public void FindBody_StopsBeforeHeadingOfHigherLevel()
    {
        int?[] levels = [2, null, 1];

        var body = SectionMap.FindBody(levels, headingIndex: 0);

        body.Count.ShouldBe(1);
    }

    [Fact]
    public void FindBody_IncludesNestedLowerLevelSubsections()
    {
        // # A / Alpha / ## A1 / Sub  — folding A owns Alpha, A1 and Sub.
        int?[] levels = [1, null, 2, null];

        var body = SectionMap.FindBody(levels, headingIndex: 0);

        body.Start.ShouldBe(1);
        body.Count.ShouldBe(3);
    }

    [Fact]
    public void FindBody_HeadingAtEndOfDocument_HasEmptyBody()
    {
        int?[] levels = [null, 1];

        var body = SectionMap.FindBody(levels, headingIndex: 1);

        body.Start.ShouldBe(2);
        body.Count.ShouldBe(0);
    }

    [Fact]
    public void FindBody_HeadingImmediatelyFollowedBySameLevel_HasEmptyBody()
    {
        int?[] levels = [1, 1];

        var body = SectionMap.FindBody(levels, headingIndex: 0);

        body.Count.ShouldBe(0);
    }

    [Fact]
    public void FindBody_NonHeadingIndex_Throws()
    {
        int?[] levels = [1, null];

        Should.Throw<ArgumentException>(() => SectionMap.FindBody(levels, headingIndex: 1));
    }

    [Fact]
    public void FindBody_IndexOutOfRange_Throws()
    {
        int?[] levels = [1];

        Should.Throw<ArgumentOutOfRangeException>(() => SectionMap.FindBody(levels, headingIndex: 5));
    }
}
