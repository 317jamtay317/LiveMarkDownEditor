using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for <see cref="OutlineView"/>, the pure computation behind the Navigation Panel's
/// Collapse/Expand: which Outline Entries lead nested entries, and which are hidden because an
/// ancestor Outline Entry is Collapsed. Entries are modelled as their heading levels (1–6) in
/// document order, with a parallel list of Collapsed flags. Underpins INV-012 — Collapsing an Outline
/// Entry hides exactly its nested Outline Entries and touches nothing else.
/// </summary>
public sealed class OutlineViewTests
{
    [Fact]
    public void HasNestedEntries_WhenNextEntryIsDeeper_IsTrue()
    {
        int[] levels = [1, 2, 1];

        OutlineView.HasNestedEntries(levels, 0).ShouldBeTrue();
    }

    [Fact]
    public void HasNestedEntries_WhenNextEntryIsSameOrHigher_IsFalse()
    {
        int[] levels = [1, 1, 2];

        OutlineView.HasNestedEntries(levels, 0).ShouldBeFalse();
    }

    [Fact]
    public void HasNestedEntries_ForLastEntry_IsFalse()
    {
        int[] levels = [1, 2];

        OutlineView.HasNestedEntries(levels, 1).ShouldBeFalse();
    }

    [Fact]
    public void VisibleEntries_WithNothingCollapsed_AreAllVisible()
    {
        int[] levels = [1, 2, 3, 1];
        bool[] collapsed = [false, false, false, false];

        OutlineView.VisibleEntries(levels, collapsed).ShouldBe([true, true, true, true]);
    }

    [Fact]
    public void VisibleEntries_UnderCollapsedAncestor_AreHidden()
    {
        // # A (collapsed) / ## A1 / ### A1a / # B  — A's descendants hide, B stays.
        int[] levels = [1, 2, 3, 1];
        bool[] collapsed = [true, false, false, false];

        OutlineView.VisibleEntries(levels, collapsed).ShouldBe([true, false, false, true]);
    }

    [Fact]
    public void VisibleEntries_WithNestedCollapse_HidesOnlyTheDeeperEntries()
    {
        // # A / ## A1 (collapsed) / ### A1a / ## A2  — only A1a hides; A2 (a sibling) stays.
        int[] levels = [1, 2, 3, 2];
        bool[] collapsed = [false, true, false, false];

        OutlineView.VisibleEntries(levels, collapsed).ShouldBe([true, true, false, true]);
    }

    [Fact]
    public void VisibleEntries_CollapsingALeafEntry_HidesNothing()
    {
        // A collapsed flag on an entry that leads no nested entries has no effect.
        int[] levels = [1, 2, 2];
        bool[] collapsed = [false, true, false];

        OutlineView.VisibleEntries(levels, collapsed).ShouldBe([true, true, true]);
    }

    [Fact]
    public void VisibleEntries_MismatchedLengths_Throws()
    {
        int[] levels = [1, 2];
        bool[] collapsed = [false];

        Should.Throw<ArgumentException>(() => OutlineView.VisibleEntries(levels, collapsed));
    }
}
