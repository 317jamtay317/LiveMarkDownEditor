using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
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

    // Two offsets are the same scroll position if they are this close. Enough to absorb the rounding
    // a view applies when it snaps a requested offset to a line boundary.
    private const double SameOffset = 0.5d;

    // The offset each view was last scrolled *to* by a sync, so the ScrollChanged that follows can be
    // recognised as our own echo and not bounced back.
    //
    // A flag held across the ScrollTo call cannot do this: the induced ScrollChanged is raised at the
    // next layout pass, long after the flag has been cleared, so the echo always escaped. Held against
    // the view rather than in one static field because Tabs give one editing surface per Editor
    // Session, and a shared flag would let one Tab's sync suppress another's.
    private static readonly ConditionalWeakTable<FrameworkElement, StrongBox<double>> Induced = [];

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
        if (sender is not FrameworkElement source || GetSyncPartner(source) is not { } partner)
        {
            return;
        }

        // Horizontal-only changes have no vertical movement to mirror.
        if (e.VerticalChange == 0d && e.ExtentHeightChange == 0d && e.ViewportHeightChange == 0d)
        {
            return;
        }

        var view = ScrollableView.Of(source);

        // The echo of a sync we ourselves induced. Recognised by where the view has landed rather than
        // by a flag, because the echo arrives a layout pass later than the call that caused it.
        if (Induced.TryGetValue(source, out var induced) && Math.Abs(view.Offset - induced.Value) < SameOffset)
        {
            Induced.Remove(source);
            return;
        }

        var target = ProportionalScroll.TargetOffset(
            view.Offset,
            view.ScrollableHeight,
            ScrollableView.Of(partner).ScrollableHeight);

        // Remembered before the move, since a view may raise its ScrollChanged synchronously.
        Induced.AddOrUpdate(partner, new StrongBox<double>(target));
        ScrollableView.ScrollTo(partner, target);
    }
}
