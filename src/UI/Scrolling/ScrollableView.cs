using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace UI.Scrolling;

/// <summary>
/// A scrollable view's Viewport measurements, read the same way whichever kind of view it is: a text
/// view that scrolls its own content (the Source Panel, or the editing surface when Page View is
/// off), or a <see cref="ScrollViewer"/> that scrolls a child (the Page View canvas that scrolls the
/// Document Sheet).
/// </summary>
/// <param name="Offset">The Viewport's current vertical offset.</param>
/// <param name="ScrollableHeight">How far the view can scroll — zero when its content fits.</param>
/// <param name="ViewportHeight">The height of the Viewport itself.</param>
public readonly record struct ScrollableView(double Offset, double ScrollableHeight, double ViewportHeight)
{
    /// <summary>What a view that does not scroll at all reports.</summary>
    public static ScrollableView None => new(0d, 0d, 0d);

    /// <summary>Whether the view has anywhere to scroll to.</summary>
    public bool CanScroll => ScrollableHeight > 0d;

    /// <summary>Measures <paramref name="view"/>'s Viewport.</summary>
    /// <param name="view">The scrollable view to measure.</param>
    /// <returns>Its measurements, or <see cref="None"/> when it is not a kind of view that scrolls.</returns>
    public static ScrollableView Of(FrameworkElement? view) => view switch
    {
        // A text view reports its content's full height, so the scrollable part is what the Viewport
        // does not already show.
        TextBoxBase text => new(text.VerticalOffset, text.ExtentHeight - text.ViewportHeight, text.ViewportHeight),
        ScrollViewer scroller => new(scroller.VerticalOffset, scroller.ScrollableHeight, scroller.ViewportHeight),
        _ => None,
    };

    /// <summary>Moves <paramref name="view"/>'s Viewport to <paramref name="offset"/>.</summary>
    /// <param name="view">The scrollable view to move.</param>
    /// <param name="offset">The vertical offset to move its Viewport to.</param>
    public static void ScrollTo(FrameworkElement? view, double offset)
    {
        switch (view)
        {
            case TextBoxBase text:
                text.ScrollToVerticalOffset(offset);
                break;
            case ScrollViewer scroller:
                scroller.ScrollToVerticalOffset(offset);
                break;
        }
    }
}
