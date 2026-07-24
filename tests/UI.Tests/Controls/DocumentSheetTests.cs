using Shouldly;
using UI.Controls;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for <see cref="DocumentSheet"/>: the pure rule that fixes the Document Sheet's Page size and
/// page margins, so in Page View the Visual Document — tables included — is confined to a single page
/// width and laid out on whole US Letter Pages that the Sheet grows by as content needs them (INV-058).
/// </summary>
public sealed class DocumentSheetTests
{
    [Fact]
    public void Width_IsTheUsLetterWidth_INV058()
    {
        // US Letter is 8.5 inches wide; at 96 device-independent units per inch that is 816 units.
        DocumentSheet.Width.ShouldBe(816d);
    }

    [Fact]
    public void PageHeight_IsTheUsLetterHeight_INV058()
    {
        // US Letter is 11 inches tall; at 96 device-independent units per inch that is 1056 units.
        DocumentSheet.PageHeight.ShouldBe(1056d);
    }

    [Fact]
    public void PagePadding_InsetsTheContentFromEverySheetEdge_INV058()
    {
        var padding = DocumentSheet.PagePadding;

        padding.Left.ShouldBeGreaterThan(0d);
        padding.Top.ShouldBeGreaterThan(0d);
        padding.Right.ShouldBeGreaterThan(0d);
        padding.Bottom.ShouldBeGreaterThan(0d);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(400d)]
    [InlineData(1056d)]
    public void PageCount_GivenContentThatFitsOnePage_IsOnePage_INV058(double contentHeight)
    {
        DocumentSheet.PageCount(contentHeight).ShouldBe(1);
    }

    [Theory]
    [InlineData(1057d, 2)]
    [InlineData(2112d, 2)]
    [InlineData(2113d, 3)]
    [InlineData(5280d, 5)]
    public void PageCount_GivenContentThatOverflowsThePage_AddsTheNextPage_INV058(double contentHeight, int expected)
    {
        DocumentSheet.PageCount(contentHeight).ShouldBe(expected);
    }

    [Fact]
    public void PageCount_GivenAnUnmeasuredHeight_IsOnePage_INV058()
    {
        DocumentSheet.PageCount(double.NaN).ShouldBe(1);
        DocumentSheet.PageCount(-500d).ShouldBe(1);
    }

    [Theory]
    [InlineData(0d, 1056d)]
    [InlineData(400d, 1056d)]
    [InlineData(1056d, 1056d)]
    [InlineData(1200d, 2112d)]
    public void HeightFor_IsAlwaysAWholeNumberOfPages_INV058(double contentHeight, double expected)
    {
        DocumentSheet.HeightFor(contentHeight).ShouldBe(expected);
    }

    [Fact]
    public void TrailingSpaceFor_FillsOutTheRestOfTheLastPage_INV058()
    {
        DocumentSheet.TrailingSpaceFor(400d).ShouldBe(656d);
        DocumentSheet.TrailingSpaceFor(1056d).ShouldBe(0d);
        DocumentSheet.TrailingSpaceFor(1200d).ShouldBe(912d);
    }

    [Fact]
    public void TrailingSpaceFor_NeverAsksForNegativeSpace_INV058()
    {
        // A content height a hair over the Page — within the sub-pixel tolerance that keeps a rounding
        // overshoot from adding a whole empty Page — must still leave the Sheet a non-negative filler.
        DocumentSheet.TrailingSpaceFor(1056.4d).ShouldBe(0d);
    }
}
