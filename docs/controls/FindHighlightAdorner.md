# FindHighlightAdorner

The **FindHighlightAdorner** draws the Find highlights over the
[MarkdownRichEditor](MarkdownRichEditor.md)'s Visual Document: a translucent fill behind every
Match, with the Current Match filled more strongly and outlined. It is the visible half of Find; the
matching itself is the pure [`MatchFinder`](../../src/UI/Find/MatchFinder.cs).

- **Class:** `UI.Controls.FindHighlightAdorner` (derives from `System.Windows.Documents.Adorner`)
- **Attached by:** `MarkdownRichEditor` on `Loaded`, into the editor's `AdornerLayer`.

## Division of labour

Find is split so that only the editor Control touches the document:

1. **The editor computes.** `MarkdownRichEditor` builds a plain-text snapshot of the Visual Document
   (concatenating text runs, with a separator inserted at block boundaries so a Match never bridges
   two blocks), runs `MatchFinder` over it, and maps each Match back to a `TextRange`. It tracks
   which Match is the **Current Match** and exposes `MatchCount` / `MatchSummary` for the Find Bar.
2. **The adorner draws.** The editor calls `Update` with the Match ranges and the Current Match
   index; `OnRender` paints a rounded fill behind each on-screen Match, using a stronger fill and an
   outline for the Current Match. `SetColors` lets the editor pass palette-derived brushes so the
   highlights follow the light/dark theme.

## How it works

- **Repaint (cheap).** Scrolling and resizing only repaint from the ranges already held — they never
  re-run the search. Off-screen and wrapped Matches are skipped rather than painted.
- **View-only (INV-016).** The adorner is presentation-only (`IsHitTestVisible = false`) and only
  ever draws. Neither it nor the Find state on the editor feeds back into Capture, so finding,
  highlighting, and moving the Current Match never change the Markdown Document.
- **Stale pointers.** When the document is replaced the editor recomputes the ranges; a repaint that
  races that replacement swallows the resulting `InvalidOperationException` and waits for the fresh
  ranges.
