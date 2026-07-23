using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using UI.Scrolling;

namespace UI.Controls;

/// <summary>
/// The Scroll Sync behaviour: an attached property that keeps two scrollable views — the Visual
/// Document and the Source Panel — aligned as the user scrolls. Set <see cref="SyncPartnerProperty"/>
/// on each view to point at the other; scrolling either then proportionally scrolls the other to the
/// same fraction of its scrollable height (INV-015).
/// </summary>
/// <remarks>
/// Wiring the two views is view-interaction logic, so it lives in a behaviour rather than a View's
/// code-behind. The proportional mapping is the pure <see cref="ProportionalScroll"/>; this behaviour
/// only reads the scrolled view's offset and moves its partner's viewport — it never changes any
/// Markdown Document. A single guard flag suppresses the echo: while one view is being scrolled to
/// follow the other, that induced scroll does not drive a sync back.
/// <para>
/// A partner is either a <see cref="TextBoxBase"/> (a view that scrolls its own content, like the
/// Source Panel or the plain editing surface) or a <see cref="ScrollViewer"/> (the Page View canvas
/// that scrolls the Document Sheet). Both expose a vertical offset, extent and viewport, so the same
/// proportional mapping drives either; <see cref="PageView"/> re-points the partners at the canvas when
/// it takes the editor's scrolling out onto the canvas (INV-058).
/// </para>
/// </remarks>
public static class ScrollSync
{
    /// <summary>
    /// Identifies the <c>SyncPartner</c> attached property: the view (a <see cref="TextBoxBase"/> or a
    /// <see cref="ScrollViewer"/>) whose vertical scrolling this element mirrors, and which mirrors this
    /// element in turn.
    /// </summary>
    public static readonly DependencyProperty SyncPartnerProperty = DependencyProperty.RegisterAttached(
        "SyncPartner",
        typeof(FrameworkElement),
        typeof(ScrollSync),
        new PropertyMetadata(null, OnSyncPartnerChanged));

    // True while a partner is being scrolled to follow a scroll we are already handling, so the
    // induced ScrollChanged does not bounce back and re-drive the originating view.
    private static bool _isSyncing;

    /// <summary>Sets the Scroll Sync partner of <paramref name="element"/>.</summary>
    /// <param name="element">The scrollable view to keep in sync.</param>
    /// <param name="value">The partner view it mirrors — a <see cref="TextBoxBase"/> or a <see cref="ScrollViewer"/>.</param>
    public static void SetSyncPartner(DependencyObject element, FrameworkElement? value) =>
        element.SetValue(SyncPartnerProperty, value);

    /// <summary>Gets the Scroll Sync partner of <paramref name="element"/>.</summary>
    /// <param name="element">The scrollable view to query.</param>
    /// <returns>The partner view it mirrors, or <see langword="null"/> when Scroll Sync is not set.</returns>
    public static FrameworkElement? GetSyncPartner(DependencyObject element) =>
        (FrameworkElement?)element.GetValue(SyncPartnerProperty);

    private static void OnSyncPartnerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement view)
        {
            return;
        }

        if (e.OldValue is not null)
        {
            view.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnViewScrolled));
        }

        if (e.NewValue is not null)
        {
            view.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnViewScrolled));
        }
    }

    private static void OnViewScrolled(object sender, ScrollChangedEventArgs e)
    {
        // Ignore the echo of a scroll we ourselves induced, and horizontal-only changes (no vertical
        // movement to mirror).
        if (_isSyncing || sender is not FrameworkElement source || GetSyncPartner(source) is not { } partner)
        {
            return;
        }

        if (e.VerticalChange == 0d && e.ExtentHeightChange == 0d && e.ViewportHeightChange == 0d)
        {
            return;
        }

        var (offset, sourceScrollable) = Metrics(source);
        var (_, partnerScrollable) = Metrics(partner);
        var target = ProportionalScroll.TargetOffset(offset, sourceScrollable, partnerScrollable);

        _isSyncing = true;
        try
        {
            ScrollTo(partner, target);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    // The vertical offset and scrollable height of either kind of partner: a text view that scrolls its
    // own content, or a ScrollViewer that scrolls its child.
    private static (double Offset, double Scrollable) Metrics(FrameworkElement view) => view switch
    {
        TextBoxBase text => (text.VerticalOffset, text.ExtentHeight - text.ViewportHeight),
        ScrollViewer scroller => (scroller.VerticalOffset, scroller.ScrollableHeight),
        _ => (0d, 0d),
    };

    private static void ScrollTo(FrameworkElement view, double offset)
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
