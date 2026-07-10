using Shouldly;
using UI.Find;
using Xunit;

namespace UI.Tests.Find;

/// <summary>
/// Tests for <see cref="MatchFinder"/>: the pure text search behind Find. It locates every
/// occurrence of a query in a text snapshot — in order, case-insensitively, and never overlapping —
/// and computes wrap-around movement between Matches for Find Next / Find Previous (INV-016).
/// </summary>
public sealed class MatchFinderTests
{
    [Fact]
    public void FindMatches_EmptyQuery_FindsNothing()
    {
        MatchFinder.FindMatches("some text", string.Empty).ShouldBeEmpty();
    }

    [Fact]
    public void FindMatches_NullText_FindsNothing()
    {
        MatchFinder.FindMatches(null, "text").ShouldBeEmpty();
    }

    [Fact]
    public void FindMatches_FindsEveryOccurrence_InDocumentOrder()
    {
        var matches = MatchFinder.FindMatches("ababab", "ab");

        matches.Select(m => m.Start).ShouldBe([0, 2, 4]);
        matches.ShouldAllBe(m => m.Length == 2);
    }

    [Fact]
    public void FindMatches_IsCaseInsensitive()
    {
        var matches = MatchFinder.FindMatches("Hello hello HELLO", "hello");

        matches.Select(m => m.Start).ShouldBe([0, 6, 12]);
    }

    [Fact]
    public void FindMatches_DoesNotOverlap()
    {
        // "aaaa" contains "aa" starting at 0, 1, and 2, but non-overlapping search yields only 0 and 2.
        var matches = MatchFinder.FindMatches("aaaa", "aa");

        matches.Select(m => m.Start).ShouldBe([0, 2]);
    }

    [Fact]
    public void FindMatches_NoOccurrence_FindsNothing()
    {
        MatchFinder.FindMatches("the quick brown fox", "cat").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0, 1, 3, 1)]
    [InlineData(2, 1, 3, 0)]  // forward past the end wraps to the first
    [InlineData(0, -1, 3, 2)] // backward past the start wraps to the last
    [InlineData(1, -1, 3, 0)]
    public void Advance_WrapsAroundTheEnds(int index, int delta, int count, int expected)
    {
        MatchFinder.Advance(index, delta, count).ShouldBe(expected);
    }

    [Fact]
    public void Advance_WithNoMatches_IsNone()
    {
        // With nothing to move between, the Current Match index is "none" (-1).
        MatchFinder.Advance(index: -1, delta: 1, count: 0).ShouldBe(-1);
    }
}
