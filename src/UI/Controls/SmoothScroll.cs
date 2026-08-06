using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using UI.Scrolling;

namespace UI.Controls;

/// <summary>
/// The Smooth Scroll behaviour: an attached property that takes over the mouse wheel for a scrollable
/// view, so a Wheel Notch moves its Viewport by a Scroll Step and the Viewport travels there across
/// frames rather than jumping (INV-075).
/// </summary>
/// <remarks>
/// Wiring the wheel is view-interaction logic, so it lives in a behaviour rather than a View's
/// code-behind. All the arithmetic is the pure <see cref="ScrollStep"/>; this behaviour only reads
/// the surface's metrics and moves its Viewport, and never touches a Markdown Document.
/// <para>
/// It exists because a stock <c>ScrollViewer</c> scrolls a fixed three lines of sixteen pixels per
/// notch — about a line and a half of this editor's text — and discards the wheel delta's magnitude,
/// so spinning harder travels no further; and because it covers that distance in a single frame,
/// which reads as a jolt rather than as motion.
/// </para>
/// <para>
/// The handler is attached to <see cref="UIElement.PreviewMouseWheelEvent"/> — the tunnelling event —
/// because the editing surface's own <c>ScrollViewer</c> marks the bubbling one handled even in Page
/// View, where it has been disabled and cannot scroll. Per-surface travel state is held against the
/// scroller rather than in a static field, because Tabs give one editing surface per Editor Session
/// and a shared Scroll Target would let one Tab's gesture move another's Viewport.
/// </para>
/// </remarks>
public static class SmoothScroll
{
    // The fraction of the remaining gap the Viewport closes each frame. Chosen so a Wheel Notch takes
    // roughly eight to ten frames at 60Hz: long enough to read as movement, short enough that the
    // Viewport keeps up with a fast spin rather than lagging behind the hand.
    private const double Smoothing = 0.28d;

    // What a line is taken to be on a surface that cannot report one — a text view, or an editing
    // surface with nothing laid out yet. WPF's own default line spacing for a font size.
    private const double LineHeightFactor = 1.35d;

    /// <summary>
    /// Identifies the <c>IsEnabled</c> attached property: whether this view scrolls smoothly, by a
    /// Scroll Step, in response to the mouse wheel.
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(SmoothScroll),
        new PropertyMetadata(false, OnIsEnabledChanged));

    // One travel per scrolled view. Weakly held so a closed Tab's surface is collectable.
    private static readonly ConditionalWeakTable<FrameworkElement, Travel> Travels = [];

    /// <summary>Sets whether <paramref name="element"/> Smooth Scrolls under the mouse wheel.</summary>
    /// <param name="element">The scrollable view to take the wheel for.</param>
    /// <param name="value"><see langword="true"/> to Smooth Scroll it.</param>
    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    /// <summary>Gets whether <paramref name="element"/> Smooth Scrolls under the mouse wheel.</summary>
    /// <param name="element">The view to query.</param>
    /// <returns><see langword="true"/> when the view Smooth Scrolls.</returns>
    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement host)
        {
            return;
        }

        host.PreviewMouseWheel -= OnWheel;
        if (e.NewValue is true)
        {
            host.PreviewMouseWheel += OnWheel;
        }
    }

    private static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        // A modifier means the wheel is asking for something other than scrolling — zoom, most often —
        // so it is left for whoever wants it.
        if (Keyboard.Modifiers != ModifierKeys.None
            || sender is not FrameworkElement host
            || ScrollerOf(host) is not { } scroller)
        {
            return;
        }

        var view = ScrollableView.Of(scroller);
        if (!view.CanScroll)
        {
            // Nothing to scroll here. Leave the notch for whatever is outside us (INV-075).
            return;
        }

        var travel = Travels.GetOrCreateValue(scroller);

        // A notch arriving mid-travel moves the target on from where the travel was already heading,
        // so a burst of notches accumulates instead of each one restarting from the current offset.
        var from = travel.IsTravelling ? travel.ScrollTarget : view.Offset;
        var distance = ScrollStep.DistanceFor(
            e.Delta,
            SystemParameters.WheelScrollLines,
            LineHeightOf(host, travel),
            view.ViewportHeight);
        var target = ScrollStep.TargetFor(from, distance, view.ScrollableHeight);

        if (Math.Abs(target - view.Offset) < 0.5d)
        {
            // Already where the notch would put us — at the top or the end. Leave it unhandled so the
            // gesture can fall through to any outer scrollable chrome.
            return;
        }

        travel.ScrollTarget = target;
        Begin(travel, scroller);
        e.Handled = true;
    }

    // Starts, or lets continue, the per-frame travel toward the Scroll Target. CompositionTarget
    // .Rendering ticks once per composed frame, so the Viewport advances in step with what is drawn.
    private static void Begin(Travel travel, FrameworkElement scroller)
    {
        if (travel.IsTravelling)
        {
            return;
        }

        travel.IsTravelling = true;
        travel.Tick = (_, _) => Advance(travel, scroller);
        CompositionTarget.Rendering += travel.Tick;
    }

    private static void Advance(Travel travel, FrameworkElement scroller)
    {
        var view = ScrollableView.Of(scroller);

        // The document may have shortened under us — an edit, a Fold, a re-Project — so the target is
        // re-clamped rather than trusted to still be reachable.
        travel.ScrollTarget = ScrollStep.TargetFor(travel.ScrollTarget, 0d, view.ScrollableHeight);

        var next = ScrollStep.Settle(view.Offset, travel.ScrollTarget, Smoothing);
        ScrollableView.ScrollTo(scroller, next);

        if (next == travel.ScrollTarget)
        {
            End(travel);
        }
    }

    private static void End(Travel travel)
    {
        if (travel.Tick is { } tick)
        {
            CompositionTarget.Rendering -= tick;
        }

        travel.Tick = null;
        travel.IsTravelling = false;
    }

    // The view that actually moves. In Page View the editing surface's own scrolling is disabled and
    // the canvas scrolls the Document Sheet; with Page View off the canvas is inert and the surface
    // scrolls itself. Asking PageView rather than guessing keeps the two in step.
    private static FrameworkElement? ScrollerOf(FrameworkElement host) => host switch
    {
        ScrollViewer canvas when canvas.Content is DependencyObject surface =>
            PageView.GetIsEnabled(surface) ? canvas : PageView.GetEditor(surface),
        TextBoxBase text => text,
        _ => null,
    };

    // The surface whose text a line is measured in. This is deliberately not the view that scrolls: in
    // Page View the canvas moves, but the canvas holds no text and its font size is whatever it
    // inherited, so measuring a "line" there would give the Scroll Step a size unrelated to the words
    // on screen.
    private static FrameworkElement? TextSurfaceOf(FrameworkElement host) => host switch
    {
        ScrollViewer canvas when canvas.Content is DependencyObject surface => PageView.GetEditor(surface),
        TextBoxBase text => text,
        _ => null,
    };

    // The line height a Scroll Step is measured in: the surface's font's own line spacing, which is
    // the height of a line of its body text.
    //
    // Deliberately not the height of the document's first line. That is whatever block happens to
    // start the document — a title, most often — so a document opening with a large heading would get
    // a Scroll Step half again too big, and one opening with a caption too small, for the body text
    // the reader actually scrolls through. Reading it from the font also costs no layout resolution
    // at all, which keeps the wheel off the path INV-074 is about.
    private static double LineHeightOf(FrameworkElement host, Travel travel)
    {
        var surface = TextSurfaceOf(host);
        if (surface is not Control control)
        {
            return 0d;
        }

        var fontSize = control.FontSize;
        if (travel.LineHeight > 0d && Math.Abs(travel.MeasuredAtFontSize - fontSize) < 0.01d)
        {
            return travel.LineHeight;
        }

        // LineSpacing is the font's own line height as a multiple of its em size — 1.33 for Segoe UI,
        // which is the 19.95 a 15pt line of this editor's text actually measures.
        var spacing = control.FontFamily?.LineSpacing ?? 0d;
        travel.LineHeight = fontSize * (spacing > 0d ? spacing : LineHeightFactor);
        travel.MeasuredAtFontSize = fontSize;
        return travel.LineHeight;
    }

    // One view's travel: where its Viewport is heading, and the per-frame handler carrying it there.
    private sealed class Travel
    {
        public double ScrollTarget { get; set; }

        public bool IsTravelling { get; set; }

        public EventHandler? Tick { get; set; }

        public double LineHeight { get; set; }

        public double MeasuredAtFontSize { get; set; }
    }
}
