using System.Windows;
using System.Windows.Controls;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for <see cref="SizeObserver"/> — the behaviour that reports an element's live width into a
/// bindable attached property, so a ViewModel can drive its responsive layout off the measured width
/// (INV-059). It is the width feed behind Compact Layout.
/// </summary>
public sealed class SizeObserverTests
{
    private static void Layout(Grid grid, double width)
    {
        grid.Measure(new Size(width, 400));
        grid.Arrange(new Rect(0, 0, width, 400));
        grid.UpdateLayout();
    }

    [Fact]
    public void Observe_ReportsTheElementsWidth_INV059()
    {
        StaThread.Run(() =>
        {
            var grid = new Grid();
            var host = new Border();
            grid.Children.Add(host);
            SizeObserver.SetObserve(host, true);

            Layout(grid, 640);

            SizeObserver.GetObservedWidth(host).ShouldBe(640);
        });
    }

    [Fact]
    public void Observe_UpdatesTheReportedWidth_WhenTheElementIsResized_INV059()
    {
        StaThread.Run(() =>
        {
            var grid = new Grid();
            var host = new Border();
            grid.Children.Add(host);
            SizeObserver.SetObserve(host, true);

            Layout(grid, 640);
            Layout(grid, 300);

            SizeObserver.GetObservedWidth(host).ShouldBe(300);
        });
    }
}
