# SmoothScroll

**Smooth Scroll**: the behaviour that takes over the mouse wheel for a scrollable view so a **Wheel
Notch** moves its **Viewport** by a **Scroll Step** — three lines of *that surface's own text* — and the
Viewport **travels** there across frames instead of jumping. A wheel delta larger than one notch, which
is how Windows reports a fast spin, travels proportionally further; a sub-notch delta from a precision
touchpad travels proportionally less. It is presentation-only: it moves Viewports and never changes a
Markdown Document (INV-075).

- **Class:** `UI.Controls.SmoothScroll` (a `static` attached behaviour)
- **Rule:** `UI.Scrolling.ScrollStep` — the pure arithmetic (`DistanceFor`, `TargetFor`, `Settle`)
- **Metrics:** `UI.Scrolling.ScrollableView` — one reading of offset / scrollable height / viewport
  height for either kind of view, shared with [Scroll Sync](../Invariants.md#inv-015)

Authored as an attached behaviour — the sanctioned home for view-interaction logic outside a ViewModel
— so no wheel handling lands in `MainWindow.xaml.cs`, which stays free of code-behind. Every number the
behaviour moves by comes from `ScrollStep`, which is pure and has no WPF dependency, so the rules a
wheel gesture obeys are tested without a window.

## Why it exists

A stock WPF `ScrollViewer` does two things that read badly in a text editor:

- **It ignores how hard you spin the wheel.** `ScrollViewer.OnMouseWheel` calls `MouseWheelDown()` once
  per message and never reads `e.Delta`, so one notch and a fast five-notch spin move the same
  distance. Spinning harder covers no more ground.
- **It moves a fixed 48 device-independent pixels** — `SystemParameters.WheelScrollLines` (3) × a
  hard-coded 16px "line" — regardless of the surface's font. Against this editor's ~20px line that is
  about a line and a half per notch, which feels sluggish.
- **It covers that distance in a single frame**, so each notch is a discrete jolt with no intermediate
  positions rather than motion.

Smooth Scroll replaces all three: the step is measured in the surface's own line height, the delta's
magnitude is honoured, and the Viewport is animated onto its target.

## How it works

A `PreviewMouseWheel` handler — the **tunnelling** event, because the editing surface's own
`ScrollViewer` marks the bubbling one handled even in [Page View](PageView.md), where it has been
disabled and cannot scroll — turns each notch into a move of the **Scroll Target**:

- **It finds the view that actually moves.** In Page View the editor's own scrolling is disabled and the
  canvas scrolls the Document Sheet; with Page View off the canvas is inert and the editor scrolls
  itself. The behaviour asks `PageView` which it is rather than guessing, so the two stay in step.
- **It measures a line from the editor's font**, not from the view that scrolls and not from the
  document's first line. The canvas holds no text, and the first line is whatever block opens the
  document — usually a title — so a document starting with a large heading would get a step half again
  too big for the body text the reader actually scrolls through. `FontFamily.LineSpacing × FontSize` is
  the body line height (1.33 × 15 = 19.95 for this editor's default), and reading it from the font
  costs no layout resolution at all, keeping the wheel off the path [INV-074](../Invariants.md#inv-074)
  is about.
- **It accumulates onto the Scroll Target.** A notch arriving mid-travel moves the target further on
  rather than restarting from where the Viewport has got to, so a burst of notches sums.
- **It settles the Viewport onto the target** on `CompositionTarget.Rendering` — once per composed
  frame — closing 28% of the remaining gap each frame, which is roughly eight to ten frames per notch
  at 60Hz. Once the gap falls under a pixel the Viewport lands exactly on the target and the handler
  detaches, so the travel always terminates.
- **It leaves a notch it cannot act on unhandled** — the view has no scrollable height, or it is already
  at the limit the notch pushes toward — so the gesture falls through to any outer scrollable chrome
  instead of being swallowed. A wheel with a modifier held is left alone too, since that is asking for
  something other than scrolling.

Travel state is held per view in a `ConditionalWeakTable` keyed on the scroller, never in a static
field: Tabs give one editing surface per Editor Session, and shared state would let one Tab's gesture
move another's Viewport.

### Measured

Against a long document at the default font, per wheel gesture:

| Gesture | Stock `ScrollViewer` | Smooth Scroll |
| --- | --- | --- |
| One notch (`delta -120`) | 48.0 DIP, in one frame | 60.0 DIP, over ~9 frames |
| Fast spin (`delta -600`) | 48.0 DIP | 299.0 DIP |
| Touchpad third-notch (`delta -40`) | 48.0 DIP | 20.0 DIP |

## Attached properties

| Property | Type | Description |
| --- | --- | --- |
| `IsEnabled` | `bool` | Whether this view Smooth Scrolls under the mouse wheel. |

Set it on the view that owns the gesture — the Page View canvas (which resolves to the canvas or the
editor depending on the mode) and the Source Panel. Attach it to the **canvas**, not to the surface
`Grid` inside it: the canvas bands above, below and beside the Document Sheet are opaque and
hit-testable but are not in the Grid's subtree, so a Grid-scoped handler would give one surface two
scroll speeds.

## Usage

```xml
<ScrollViewer x:Name="EditorScroller" Focusable="False"
              controls:SmoothScroll.IsEnabled="True"
              VerticalScrollBarVisibility="Disabled" HorizontalScrollBarVisibility="Disabled">
    <!-- ... the Page View surface ... -->
</ScrollViewer>

<TextBox x:Name="SourcePanel"
         controls:ScrollSync.SyncPartner="{Binding ElementName=Editor}"
         controls:SmoothScroll.IsEnabled="True" />
```
