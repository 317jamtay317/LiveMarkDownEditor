using System.Windows;
using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for <see cref="CodeSpanShade"/>: the box drawn behind an inline Code Span, derived from the
/// two ends of the span alone so a repaint resolves two character rectangles for it rather than one
/// per character (INV-074).
/// </summary>
public sealed class CodeSpanShadeTests
{
    private const double Padding = 2d;
    private const double ViewportHeight = 600d;

    // A caret rectangle on a line 18 tall, as GetCharacterRect reports one: zero-width, at a position.
    private static Rect Caret(double left, double top) => new(left, top, 0d, 18d);

    [Fact]
    public void Box_GivenASpanOnOneLine_HugsTheTextInASingleBox_INV074()
    {
        var box = CodeSpanShade.Box(Caret(100d, 40d), Caret(180d, 40d), Padding, ViewportHeight);

        box.ShouldNotBeNull();
        box!.Value.Left.ShouldBe(98d);
        box.Value.Right.ShouldBe(182d);
        box.Value.Top.ShouldBe(40d);
        box.Value.Height.ShouldBe(18d);
    }

    [Fact]
    public void Box_GivenASpanBelowTheViewport_ShadesNothing_INV074()
    {
        // The cull that matters: a span off screen must cost nothing, not be measured and then
        // discarded.
        var box = CodeSpanShade.Box(Caret(100d, 900d), Caret(180d, 900d), Padding, ViewportHeight);

        box.ShouldBeNull();
    }

    [Fact]
    public void Box_GivenASpanAboveTheViewport_ShadesNothing_INV074()
    {
        var box = CodeSpanShade.Box(Caret(100d, -300d), Caret(180d, -300d), Padding, ViewportHeight);

        box.ShouldBeNull();
    }

    [Fact]
    public void Box_GivenASpanStraddlingTheViewportTop_StillShades_INV074()
    {
        // Partly on screen is on screen — its bottom edge reaches into the Viewport.
        var box = CodeSpanShade.Box(Caret(100d, -8d), Caret(180d, -8d), Padding, ViewportHeight);

        box.ShouldNotBeNull();
    }

    [Fact]
    public void Box_GivenAWrappedSpan_DefersToTheLineWalk_INV074()
    {
        // Two ends on different visual lines cannot be one box; the caller falls back to walking the
        // span line by line.
        var box = CodeSpanShade.Box(Caret(400d, 40d), Caret(60d, 62d), Padding, ViewportHeight);

        box.ShouldBeNull();
    }

    [Fact]
    public void Box_GivenReversedEdges_ShadesBetweenThemRatherThanThrowing_INV074()
    {
        // A degenerate or bidi-reversed span can resolve its end to the left of its start. A negative
        // width throws inside Rect's constructor, and an exception raised during a render takes the
        // app down — so the edges are ordered rather than subtracted, and the span still gets shaded.
        Rect? box = null;
        Should.NotThrow(() => box = CodeSpanShade.Box(Caret(180d, 40d), Caret(100d, 40d), Padding, ViewportHeight));

        box.ShouldNotBeNull();
        box!.Value.Width.ShouldBePositive();
        box.Value.Left.ShouldBe(98d);
        box.Value.Right.ShouldBe(182d);
    }

    [Fact]
    public void Box_GivenAnUnresolvedEnd_ShadesNothing_INV074()
    {
        CodeSpanShade.Box(Rect.Empty, Caret(180d, 40d), Padding, ViewportHeight).ShouldBeNull();
        CodeSpanShade.Box(Caret(100d, 40d), Rect.Empty, Padding, ViewportHeight).ShouldBeNull();
    }

    [Fact]
    public void Box_GivenAnEmptySpan_ShadesNothing_INV074()
    {
        // Both ends at the same position: nothing to shade, and a zero-width box would draw a sliver.
        var box = CodeSpanShade.Box(Caret(100d, 40d), Caret(100d, 40d), padding: 0d, ViewportHeight);

        box.ShouldBeNull();
    }

    [Fact]
    public void Box_GivenEndsOnTheSameLineOfDifferingHeight_TakesTheTallest_INV074()
    {
        var start = new Rect(100d, 40d, 0d, 18d);
        var end = new Rect(180d, 40d, 0d, 24d);

        var box = CodeSpanShade.Box(start, end, Padding, ViewportHeight);

        box!.Value.Height.ShouldBe(24d);
    }
}
