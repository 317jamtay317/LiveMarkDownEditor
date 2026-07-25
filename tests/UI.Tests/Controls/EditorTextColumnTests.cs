using System.Windows;
using Shouldly;
using UI.Controls;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for <see cref="EditorTextColumn"/>: where the editing surface's text column ends, which is
/// where a full-width block shade — Code Shading (INV-017) and the Change Highlight (INV-060) —
/// must stop. Its left edge already lands on the column, so getting the right edge from the
/// viewport rather than the control's full width is what makes the shade evenly inset.
/// </summary>
/// <remarks>
/// The fixtures are measured from the real control rather than invented: at 824px wide with the
/// shipped <c>20,16</c> padding, WPF gives the RichTextBox a <c>5,0,5,0</c> page padding and a
/// viewport of 784 with no scrollbar, 767 with one. Prose was observed to wrap at 770.08 and 779.77
/// respectively — just inside the column edges asserted here.
/// </remarks>
public sealed class EditorTextColumnTests
{
    private static readonly Thickness Padding = new(20, 16, 20, 16);
    private static readonly Thickness PagePadding = new(5, 0, 5, 0);

    [Fact]
    public void RightEdge_WithNoScrollBar_EndsAtTheTextColumn()
    {
        EditorTextColumn.RightEdge(actualWidth: 824, Padding, PagePadding, viewportWidth: 784)
            .ShouldBe(799);
    }

    [Fact]
    public void RightEdge_WithAScrollBar_StopsShortOfIt()
    {
        // The viewport shrinks by the scrollbar's width, so the column does too — without this the
        // shade would run underneath the scrollbar.
        EditorTextColumn.RightEdge(actualWidth: 824, Padding, PagePadding, viewportWidth: 767)
            .ShouldBe(782);
    }

    [Fact]
    public void RightEdge_LeavesTheSameGapAsTheLeft_INV017()
    {
        // The point of the change. The text column starts at Padding.Left + PagePadding.Left = 25,
        // so a 824-wide surface with symmetric padding must end 25 from its right edge too.
        var left = Padding.Left + PagePadding.Left;
        var right = EditorTextColumn.RightEdge(actualWidth: 824, Padding, PagePadding, viewportWidth: 784);

        (824 - right).ShouldBe(left);
    }

    [Fact]
    public void RightEdge_WithAPrintMargin_HonoursIt()
    {
        // Page View sets the control's padding to the Page's Margins, so a one-inch right margin has
        // to keep the shade a full inch clear of the page edge — the case that looked worst.
        var margins = new Thickness(96, 96, 96, 96);

        EditorTextColumn.RightEdge(actualWidth: 816, margins, PagePadding, viewportWidth: 816 - 192)
            .ShouldBe(715);
    }

    [Fact]
    public void RightEdge_WithNoViewport_FallsBackToTheControlWidth()
    {
        // Before the template is applied there is no ScrollViewer to ask; the control's own padding
        // still gives a sane column, just without any scrollbar allowance.
        EditorTextColumn.RightEdge(actualWidth: 824, Padding, PagePadding, viewportWidth: null)
            .ShouldBe(799);
    }

    [Fact]
    public void RightEdge_WithAutoPagePadding_TreatsItAsNone()
    {
        // FlowDocument.PagePadding defaults to Auto (NaN); it must not poison the arithmetic.
        var auto = new Thickness(double.NaN, double.NaN, double.NaN, double.NaN);

        EditorTextColumn.RightEdge(actualWidth: 824, Padding, auto, viewportWidth: 784)
            .ShouldBe(804);
    }

    [Fact]
    public void RightEdge_IsNeverLeftOfTheColumnStart()
    {
        // A surface narrower than its own padding must not produce an inverted box.
        EditorTextColumn.RightEdge(actualWidth: 10, Padding, PagePadding, viewportWidth: 0)
            .ShouldBeGreaterThanOrEqualTo(Padding.Left);
    }
}
