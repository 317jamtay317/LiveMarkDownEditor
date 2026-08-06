using Shouldly;
using UI.Scrolling;
using Xunit;

namespace UI.Tests.Scrolling;

/// <summary>
/// Tests for <see cref="ExtentIndex"/> and the <see cref="ViewportSlice"/> it yields: the pure
/// narrowing of a document-ordered set to the run whose Document Extents reach into the Viewport,
/// which is what bounds a repaint's cost to what is on screen (INV-074).
/// </summary>
public sealed class ViewportSliceTests
{
    // A document of ten single-line spans stacked twenty units apart: 0..10, 20..30, 40..50, and so on.
    private static ExtentIndex TenStackedLines() =>
        ExtentIndex.Build([.. Enumerable.Range(0, 10).Select(i => new DocumentExtent(i * 20d, (i * 20d) + 10d))]);

    [Fact]
    public void Slice_GivenAnEmptySet_IsEmpty_INV074()
    {
        var slice = ExtentIndex.Build([]).Slice(viewportTop: 0d, viewportBottom: 100d);

        slice.IsEmpty.ShouldBeTrue();
        slice.Count.ShouldBe(0);
    }

    [Fact]
    public void Slice_AtTheTopOfTheDocument_TakesTheLeadingRun_INV074()
    {
        // A Viewport 50 tall reaches the spans at 0, 20 and 40 — and no further.
        var slice = TenStackedLines().Slice(viewportTop: 0d, viewportBottom: 50d);

        slice.First.ShouldBe(0);
        slice.Count.ShouldBe(3);
    }

    [Fact]
    public void Slice_ScrolledDown_TakesOnlyTheRunOnScreen_INV074()
    {
        // Scrolled to 100..150: the spans at 100, 120 and 140. Everything above is skipped rather than
        // walked, which is the whole point of the slice.
        var slice = TenStackedLines().Slice(viewportTop: 100d, viewportBottom: 150d);

        slice.First.ShouldBe(5);
        slice.Count.ShouldBe(3);
    }

    [Fact]
    public void Slice_ScrolledPastTheEnd_IsEmpty_INV074()
    {
        TenStackedLines().Slice(viewportTop: 500d, viewportBottom: 600d).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Slice_IncludesASpanStraddlingTheTopEdge_INV074()
    {
        // The span at 100..110 is half above the Viewport top; it is still on screen and must be drawn.
        var slice = TenStackedLines().Slice(viewportTop: 105d, viewportBottom: 150d);

        slice.First.ShouldBe(5);
        slice.Count.ShouldBe(3);
    }

    [Fact]
    public void Slice_IncludesASpanStraddlingTheBottomEdge_INV074()
    {
        var slice = TenStackedLines().Slice(viewportTop: 100d, viewportBottom: 145d);

        slice.First.ShouldBe(5);
        slice.Count.ShouldBe(3);
    }

    [Fact]
    public void Slice_IncludesATallSpanStartingFarAboveTheViewport_INV074()
    {
        // A Code Block taller than the Viewport runs 0..500. Scrolled into the middle of it, neither
        // edge is on screen — but its shade still covers the Viewport, so a slice that dropped it would
        // leave the block unshaded while the user reads it.
        var index = ExtentIndex.Build(
        [
            new DocumentExtent(0d, 500d),
            new DocumentExtent(510d, 520d),
            new DocumentExtent(530d, 540d),
        ]);

        var slice = index.Slice(viewportTop: 200d, viewportBottom: 260d);

        slice.First.ShouldBe(0);
        slice.Count.ShouldBe(1);
    }

    [Fact]
    public void Slice_GivenATallSpanAmongShortOnes_KeepsTheRunContiguous_INV074()
    {
        // The tall span at index 0 reaches the Viewport, and the short spans at 200 and 220 sit in it.
        // The spans between are carried along so the slice stays one unbroken run — drawing a few extra
        // off-screen spans is cheap; hunting for scattered ones is not.
        var index = ExtentIndex.Build(
        [
            new DocumentExtent(0d, 400d),
            new DocumentExtent(100d, 110d),
            new DocumentExtent(200d, 210d),
            new DocumentExtent(220d, 230d),
            new DocumentExtent(600d, 610d),
        ]);

        var slice = index.Slice(viewportTop: 195d, viewportBottom: 260d);

        slice.First.ShouldBe(0);
        slice.Last.ShouldBe(3);
        slice.Count.ShouldBe(4);
    }

    [Fact]
    public void Slice_GivenAZeroHeightViewport_IsEmpty_INV074()
    {
        // A pane collapsed to nothing shows nothing; it must not fall back to drawing the document.
        TenStackedLines().Slice(viewportTop: 100d, viewportBottom: 100d).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Slice_NeverWalksTheWholeSetToFindTheRun_INV074()
    {
        // The guarantee behind INV-074: locating the slice is a pair of binary searches, so the extents
        // inspected grow with the logarithm of the set rather than its length. A hundred thousand spans
        // must be narrowed by inspecting a few dozen.
        var index = ExtentIndex.Build(
            [.. Enumerable.Range(0, 100_000).Select(i => new DocumentExtent(i * 20d, (i * 20d) + 10d))]);

        var slice = index.Slice(viewportTop: 1_000_000d, viewportBottom: 1_000_055d);

        slice.First.ShouldBe(50_000);
        slice.Count.ShouldBe(3);
        index.ProbesOfLastSlice.ShouldBeLessThan(100);
    }

    [Fact]
    public void Of_OverComparablePositions_TakesTheRunOnScreen_INV074()
    {
        // The adorners slice over TextPointers rather than heights, because comparing two positions
        // resolves no layout and so costs a repaint nothing. Integers stand in for positions here.
        int[] starts = [0, 100, 200, 220, 600];
        int[] furthestEnds = [400, 400, 400, 400, 610];

        var slice = ExtentIndex.Of(starts, furthestEnds, 195, 260, static (a, b) => a.CompareTo(b));

        slice.First.ShouldBe(0);
        slice.Last.ShouldBe(3);
    }

    [Fact]
    public void Of_OverComparablePositions_KeepsATallSpanWhoseEdgesAreBothOffScreen_INV074()
    {
        int[] starts = [0, 510, 530];
        int[] furthestEnds = [500, 520, 540];

        var slice = ExtentIndex.Of(starts, furthestEnds, 200, 260, static (a, b) => a.CompareTo(b));

        slice.First.ShouldBe(0);
        slice.Count.ShouldBe(1);
    }

    [Fact]
    public void Of_OverComparablePositions_GivenNothingOnScreen_IsEmpty_INV074()
    {
        int[] starts = [0, 20, 40];
        int[] furthestEnds = [10, 30, 50];

        ExtentIndex.Of(starts, furthestEnds, 500, 600, static (a, b) => a.CompareTo(b))
            .IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Build_GivenSpansOutOfDocumentOrder_OrdersThemByTop_INV074()
    {
        // The callers build their sets by walking the document, so they arrive ordered — but an index
        // that quietly assumed it would mis-slice rather than fail, and a wrong slice is an overlay
        // drawn in the wrong place.
        var index = ExtentIndex.Build(
        [
            new DocumentExtent(200d, 210d),
            new DocumentExtent(0d, 10d),
            new DocumentExtent(100d, 110d),
        ]);

        var slice = index.Slice(viewportTop: 95d, viewportBottom: 115d);

        slice.Count.ShouldBe(1);
        index.ExtentAt(slice.First).Top.ShouldBe(100d);

        // The caller draws from its own list, so the slice must lead back to the position the span was
        // handed in at — here the third one.
        index.SourceIndexAt(slice.First).ShouldBe(2);
    }
}
