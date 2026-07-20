using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for <see cref="CommandBarPanel"/>: it shows every group's individual icons while they fit, and
/// collapses a group to its dropdown — lowest CollapseOrder first — only when the row would overflow,
/// keeping any right-docked item against the right edge (INV-054).
/// </summary>
public sealed class CommandBarPanelTests
{
    private static FrameworkElement Item(double width) => new Border { Width = width, Height = 30 };

    private static FrameworkElement Grouped(double width, string group, bool overflow, int order)
    {
        var element = Item(width);
        CommandBarPanel.SetOverflowGroup(element, group);
        CommandBarPanel.SetIsOverflow(element, overflow);
        CommandBarPanel.SetCollapseOrder(element, order);
        return element;
    }

    private static void Layout(CommandBarPanel panel, double width)
    {
        panel.Measure(new Size(width, 40));
        panel.Arrange(new Rect(0, 0, width, 40));
        panel.UpdateLayout();
    }

    [Fact]
    public void WhenEverythingFits_TheExpandedIconsShow_AndTheDropdownIsHidden_INV054()
    {
        StaThread.Run(() =>
        {
            var panel = new CommandBarPanel();
            var expanded = Grouped(200, "insert", overflow: false, order: 0);
            var dropdown = Grouped(40, "insert", overflow: true, order: 0);
            panel.Children.Add(Item(100));
            panel.Children.Add(expanded);
            panel.Children.Add(dropdown);

            Layout(panel, 1000);

            expanded.Opacity.ShouldBe(1);
            dropdown.Opacity.ShouldBe(0);
        });
    }

    [Fact]
    public void WhenTooNarrow_TheGroupCollapsesToItsDropdown_INV054()
    {
        StaThread.Run(() =>
        {
            var panel = new CommandBarPanel();
            var expanded = Grouped(200, "insert", overflow: false, order: 0);
            var dropdown = Grouped(40, "insert", overflow: true, order: 0);
            panel.Children.Add(Item(100));
            panel.Children.Add(expanded);
            panel.Children.Add(dropdown);

            // 100 + 200 = 300 does not fit 150; collapsed, 100 + 40 = 140 does.
            Layout(panel, 150);

            expanded.Opacity.ShouldBe(0);
            dropdown.Opacity.ShouldBe(1);
        });
    }

    [Fact]
    public void CollapsesTheLowestCollapseOrderGroupFirst_INV054()
    {
        StaThread.Run(() =>
        {
            var panel = new CommandBarPanel();
            var earlyExpanded = Grouped(150, "early", overflow: false, order: 0);
            var earlyDropdown = Grouped(30, "early", overflow: true, order: 0);
            var lateExpanded = Grouped(150, "late", overflow: false, order: 1);
            var lateDropdown = Grouped(30, "late", overflow: true, order: 1);
            panel.Children.Add(earlyExpanded);
            panel.Children.Add(earlyDropdown);
            panel.Children.Add(lateExpanded);
            panel.Children.Add(lateDropdown);

            // Fully expanded is 300. At 200, collapsing only 'early' (30 + 150 = 180) is enough.
            Layout(panel, 200);

            earlyExpanded.Opacity.ShouldBe(0);
            earlyDropdown.Opacity.ShouldBe(1);
            lateExpanded.Opacity.ShouldBe(1);
            lateDropdown.Opacity.ShouldBe(0);
        });
    }

    [Fact]
    public void AGroupOfSeveralIcons_CollapsesAsAWhole_INV054()
    {
        StaThread.Run(() =>
        {
            var panel = new CommandBarPanel();
            var iconA = Grouped(80, "insert", overflow: false, order: 0);
            var iconB = Grouped(80, "insert", overflow: false, order: 0);
            var iconC = Grouped(80, "insert", overflow: false, order: 0);
            var dropdown = Grouped(40, "insert", overflow: true, order: 0);
            panel.Children.Add(iconA);
            panel.Children.Add(iconB);
            panel.Children.Add(iconC);
            panel.Children.Add(dropdown);

            // The three 80-wide icons (240) do not fit 100; the whole group collapses to the 40 dropdown.
            Layout(panel, 100);

            iconA.Opacity.ShouldBe(0);
            iconB.Opacity.ShouldBe(0);
            iconC.Opacity.ShouldBe(0);
            dropdown.Opacity.ShouldBe(1);
        });
    }

    [Fact]
    public void ARightDockedItem_IsLaidOutAtTheRightEdge_INV054()
    {
        StaThread.Run(() =>
        {
            var panel = new CommandBarPanel();
            var left = Item(100);
            var right = Item(50);
            DockPanel.SetDock(right, Dock.Right);
            panel.Children.Add(left);
            panel.Children.Add(right);

            Layout(panel, 400);

            LayoutInformation.GetLayoutSlot(left).X.ShouldBe(0);
            LayoutInformation.GetLayoutSlot(right).X.ShouldBe(350);
        });
    }
}
