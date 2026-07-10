using Shouldly;
using UI.Scrolling;
using Xunit;

namespace UI.Tests.Scrolling;

/// <summary>
/// Tests for <see cref="ProportionalScroll"/>: the pure mapping behind Scroll Sync that turns one
/// view's scroll offset into the partner view's offset at the same fraction of scrollable height
/// (INV-015).
/// </summary>
public sealed class ProportionalScrollTests
{
    [Fact]
    public void TargetOffset_AtTop_MapsToTop()
    {
        ProportionalScroll.TargetOffset(sourceOffset: 0d, sourceScrollableHeight: 100d, targetScrollableHeight: 200d)
            .ShouldBe(0d);
    }

    [Fact]
    public void TargetOffset_AtBottom_MapsToBottom()
    {
        // The source scrolled to its maximum maps to the target's maximum, so the bottoms align.
        ProportionalScroll.TargetOffset(sourceOffset: 100d, sourceScrollableHeight: 100d, targetScrollableHeight: 200d)
            .ShouldBe(200d);
    }

    [Fact]
    public void TargetOffset_AtMidpoint_MapsToMidpoint()
    {
        ProportionalScroll.TargetOffset(sourceOffset: 50d, sourceScrollableHeight: 100d, targetScrollableHeight: 200d)
            .ShouldBe(100d);
    }

    [Fact]
    public void TargetOffset_WhenSourceHasNoScrollableHeight_IsZero_NoDivideByZero()
    {
        // The source's content fits (nothing to scroll), so there is no meaningful fraction: the
        // partner is left at the top rather than dividing by zero.
        ProportionalScroll.TargetOffset(sourceOffset: 0d, sourceScrollableHeight: 0d, targetScrollableHeight: 200d)
            .ShouldBe(0d);
    }

    [Fact]
    public void TargetOffset_WhenTargetHasNoScrollableHeight_IsZero()
    {
        ProportionalScroll.TargetOffset(sourceOffset: 40d, sourceScrollableHeight: 100d, targetScrollableHeight: 0d)
            .ShouldBe(0d);
    }

    [Fact]
    public void TargetOffset_ClampsOvershootToTheTargetMaximum()
    {
        // A source offset beyond its own maximum (transient during layout) never drives the partner
        // past its maximum.
        ProportionalScroll.TargetOffset(sourceOffset: 150d, sourceScrollableHeight: 100d, targetScrollableHeight: 200d)
            .ShouldBe(200d);
    }
}
