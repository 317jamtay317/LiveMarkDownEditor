using Shouldly;
using UI.Scrolling;
using Xunit;

namespace UI.Tests.Scrolling;

/// <summary>
/// Tests for <see cref="ScrollStep"/>: the arithmetic that turns a Wheel Notch into a Scroll Step, a
/// Scroll Step into a Scroll Target, and a Scroll Target into the Viewport's settled approach to it
/// (INV-075).
/// </summary>
public sealed class ScrollStepTests
{
    // A surface whose lines are 20 units tall, in a Viewport 600 tall — the editing surface's own
    // proportions at its default font size.
    private const double LineHeight = 20d;
    private const double ViewportHeight = 600d;
    private const int SystemLines = 3;

    [Fact]
    public void DistanceFor_GivenOneWheelNotch_MovesByTheConfiguredLines_INV075()
    {
        // Three lines of the surface's own height — not the fixed 48 units a stock ScrollViewer moves.
        var distance = ScrollStep.DistanceFor(-ScrollStep.NotchDelta, SystemLines, LineHeight, ViewportHeight);

        distance.ShouldBe(60d);
    }

    [Fact]
    public void DistanceFor_GivenACoalescedSpin_MovesProportionally_INV075()
    {
        // Windows coalesces a fast spin into one larger delta. Five notches must travel five notches:
        // this is what makes spinning harder cover more ground.
        var distance = ScrollStep.DistanceFor(-5d * ScrollStep.NotchDelta, SystemLines, LineHeight, ViewportHeight);

        distance.ShouldBe(300d);
    }

    [Fact]
    public void DistanceFor_GivenASubNotchTouchpadDelta_MovesAFraction_INV075()
    {
        // A precision touchpad reports a third of a notch; it travels a third of a step rather than
        // being rounded up to a whole one.
        var distance = ScrollStep.DistanceFor(-40d, SystemLines, LineHeight, ViewportHeight);

        distance.ShouldBe(20d);
    }

    [Fact]
    public void DistanceFor_WhenTheSystemAsksForAScreenAtATime_MovesByAViewport_INV075()
    {
        // "One screen at a time" reaches .NET as a non-positive line count. Multiplying by it would
        // invert the travel, so the sign matters as much as the magnitude here.
        var distance = ScrollStep.DistanceFor(-ScrollStep.NotchDelta, -1, LineHeight, ViewportHeight);

        distance.ShouldBePositive();
        distance.ShouldBe(540d);
    }

    [Fact]
    public void DistanceFor_WhenTheWheelTurnsUp_MovesTowardTheTopOfTheDocument_INV075()
    {
        // A positive delta is the wheel turning away from the user, which reads as moving up the
        // document — a decreasing offset.
        var distance = ScrollStep.DistanceFor(ScrollStep.NotchDelta, SystemLines, LineHeight, ViewportHeight);

        distance.ShouldBe(-60d);
    }

    [Fact]
    public void DistanceFor_GivenASurfaceWithNoMeasuredLine_FallsBackToTheViewport_INV075()
    {
        // A surface that has not laid out yet reports no line height; a notch still has to do
        // something, and a screen is the only honest answer available.
        var distance = ScrollStep.DistanceFor(-ScrollStep.NotchDelta, SystemLines, 0d, ViewportHeight);

        distance.ShouldBe(540d);
    }

    [Fact]
    public void TargetFor_WhenNotchesArriveDuringATravel_Accumulate_INV075()
    {
        // Two notches in a burst sum onto the Scroll Target rather than the second restarting the
        // travel — the difference between a spin covering ground and a spin fighting itself.
        var first = ScrollStep.TargetFor(currentTarget: 0d, distance: 60d, scrollableHeight: 5000d);
        var second = ScrollStep.TargetFor(first, distance: 60d, scrollableHeight: 5000d);

        first.ShouldBe(60d);
        second.ShouldBe(120d);
    }

    [Fact]
    public void TargetFor_AtTheTopOfTheDocument_StopsAtTheTop_INV075()
    {
        var target = ScrollStep.TargetFor(currentTarget: 30d, distance: -500d, scrollableHeight: 5000d);

        target.ShouldBe(0d);
    }

    [Fact]
    public void TargetFor_AtTheEndOfTheDocument_StopsAtTheEnd_INV075()
    {
        var target = ScrollStep.TargetFor(currentTarget: 4900d, distance: 500d, scrollableHeight: 5000d);

        target.ShouldBe(5000d);
    }

    [Fact]
    public void TargetFor_GivenASurfaceThatCannotScroll_StaysAtTheTop_INV075()
    {
        // A negative scrollable height is what a surface shorter than its Viewport reports.
        var target = ScrollStep.TargetFor(currentTarget: 0d, distance: 60d, scrollableHeight: -40d);

        target.ShouldBe(0d);
    }

    [Fact]
    public void Settle_ClosesAFractionOfTheRemainingGap_INV075()
    {
        var offset = ScrollStep.Settle(currentOffset: 0d, scrollTarget: 100d, smoothing: 0.25d);

        offset.ShouldBe(25d);
    }

    [Fact]
    public void Settle_WhenTheGapIsUnderAPixel_LandsExactlyOnTheTarget_INV075()
    {
        // Without this the approach only ever closes a fraction, the Viewport never arrives, and the
        // per-frame handler driving it never detaches.
        var offset = ScrollStep.Settle(currentOffset: 99.7d, scrollTarget: 100d, smoothing: 0.25d);

        offset.ShouldBe(100d);
    }

    [Fact]
    public void Settle_TravellingUpward_ClosesTheGapToo_INV075()
    {
        var offset = ScrollStep.Settle(currentOffset: 100d, scrollTarget: 0d, smoothing: 0.25d);

        offset.ShouldBe(75d);
    }

    [Fact]
    public void Settle_RepeatedlyApplied_ArrivesAtTheTarget_INV075()
    {
        // The travel must terminate: a Viewport released 600 from its target lands on it within the
        // handful of frames a Wheel Notch is allowed to take.
        var offset = 0d;
        var frames = 0;
        while (offset != 600d && frames < 100)
        {
            offset = ScrollStep.Settle(offset, scrollTarget: 600d, smoothing: 0.28d);
            frames++;
        }

        offset.ShouldBe(600d);
        frames.ShouldBeLessThan(40);
    }
}
