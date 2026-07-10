using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using UI.Scrolling;

namespace UI.Controls;

/// <summary>
/// The Scroll Sync behaviour: an attached property that keeps two scrollable text views — the Visual
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
/// </remarks>
public static class ScrollSync
{
    /// <summary>
    /// Identifies the <c>SyncPartner</c> attached property: the <see cref="TextBoxBase"/> whose
    /// vertical scrolling this element mirrors, and which mirrors this element in turn.
    /// </summary>
    public static readonly DependencyProperty SyncPartnerProperty = DependencyProperty.RegisterAttached(
        "SyncPartner",
        typeof(TextBoxBase),
        typeof(ScrollSync),
        new PropertyMetadata(null, OnSyncPartnerChanged));

    // True while a partner is being scrolled to follow a scroll we are already handling, so the
    // induced ScrollChanged does not bounce back and re-drive the originating view.
    private static bool _isSyncing;

    /// <summary>Sets the Scroll Sync partner of <paramref name="element"/>.</summary>
    /// <param name="element">The scrollable text view to keep in sync.</param>
    /// <param name="value">The partner view it mirrors.</param>
    public static void SetSyncPartner(DependencyObject element, TextBoxBase? value) =>
        element.SetValue(SyncPartnerProperty, value);

    /// <summary>Gets the Scroll Sync partner of <paramref name="element"/>.</summary>
    /// <param name="element">The scrollable text view to query.</param>
    /// <returns>The partner view it mirrors, or <see langword="null"/> when Scroll Sync is not set.</returns>
    public static TextBoxBase? GetSyncPartner(DependencyObject element) =>
        (TextBoxBase?)element.GetValue(SyncPartnerProperty);

    private static void OnSyncPartnerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBoxBase view)
        {
            return;
        }

        if (e.OldValue is not null)
        {
            view.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnViewScrolled));
        }

        if (e.NewValue is TextBoxBase)
        {
            view.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnViewScrolled));
        }
    }

    private static void OnViewScrolled(object sender, ScrollChangedEventArgs e)
    {
        // Ignore the echo of a scroll we ourselves induced, and horizontal-only changes (no vertical
        // movement to mirror).
        if (_isSyncing || sender is not TextBoxBase source || GetSyncPartner(source) is not { } partner)
        {
            return;
        }

        if (e.VerticalChange == 0d && e.ExtentHeightChange == 0d && e.ViewportHeightChange == 0d)
        {
            return;
        }

        var target = ProportionalScroll.TargetOffset(
            source.VerticalOffset,
            source.ExtentHeight - source.ViewportHeight,
            partner.ExtentHeight - partner.ViewportHeight);

        _isSyncing = true;
        try
        {
            partner.ScrollToVerticalOffset(target);
        }
        finally
        {
            _isSyncing = false;
        }
    }
}
