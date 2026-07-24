# ChangeHighlightAdorner

The **ChangeHighlightAdorner** draws the Change Highlight over the
[MarkdownRichEditor](MarkdownRichEditor.md)'s Visual Document after a live reload: a shade behind
every Block another writer added or altered, and a thin tick at the seam of every run they deleted.
It is what makes "another user or an AI just edited your file" something the reader can *see* rather
than something that lands invisibly (INV-060).

- **Class:** `UI.Controls.ChangeHighlightAdorner` (derives from `System.Windows.Documents.Adorner`)
- **Attached by:** `MarkdownRichEditor` on `Loaded`, into the editor's `AdornerLayer` — **first** of
  the editor's overlays, so its shade sits beneath the Code Shading, the spelling squiggles, and the
  Find highlights rather than washing them out.
- **Driven by:** `MarkdownRichEditor.ChangedRegions`, bound to `EditorSessionViewModel.ChangeHighlight`.

## Division of labour

The Change Highlight is split three ways, so only the editor Control ever touches the document:

1. **The Domain compares.** [`ReloadDifference.Compute`](../../src/Domain/ReloadDifference.cs) maps the
   Markdown Document as the session held it and the contents that replaced it to the ordered
   **Changed Regions** — runs of the *reloaded* text that were added or altered, and the empty seams
   where content was deleted. It is pure: no state, no I/O, no UI.
2. **The scanner resolves.** [`ChangeHighlightScanner.Scan`](../../src/UI/Wysiwyg/ChangeHighlightScanner.cs)
   turns those line numbers into Blocks, using the **Source Line Range** that
   [`SourceLines`](../../src/UI/Wysiwyg/SourceLines.cs) records on each Block at projection time. A
   Changed region shades every Block it intersects; a Removed seam anchors to the first Block that
   starts at or after it, or below the last Block when the deletion fell at the end.
3. **The adorner draws.** `Show` resolves the targets and starts the hold-then-fade; `OnRender` paints
   each on-screen target from the live palette brushes.

## How it looks

- **A changed Block** gets a soft rounded panel over its whole extent in `ChangeHighlightBrush`, with
  a 3px bar down its leading edge in `ChangeHighlightMarkerBrush` so the change still reads at a
  glance on a busy page.
- **A deletion seam** gets a short 2px tick in `ChangeHighlightMarkerBrush`, between the Blocks that
  closed over the deleted content. There is nothing left to shade, so the mark says "something was
  here" without inventing content.
- Both brushes come from the active palette, so the highlight follows the light/dark theme — and,
  like Code Shading, recolouring an overlay that owns no text can never reflow the document.

## How it behaves

- **Hold, then fade.** The overlay holds at full strength for 2.2s and then fades over 0.9s, animated
  on `Opacity` with `FillBehavior.Stop`. Nothing has to be dismissed, and it can never be mistaken for
  permanent document state.
- **It moves nothing.** No caret, no selection, no scrolling — deliberately. The reload is another
  writer's action, and taking the reader's place away mid-read is not something they asked for. A
  change that lands off-screen simply is not seen, which is the accepted cost of not stealing focus.
- **Never stale (INV-060).** The Editor Session clears `ChangeHighlight` on an edit, a load, or a
  save, which pushes an empty set and takes the overlay down early. A highlight therefore always
  refers to a change that is actually on screen.
- **Painted after layout.** The regions arrive immediately after the reloaded source, when the new
  document has been projected but not yet laid out — every character rectangle would still be empty.
  `Show` therefore queues one repaint at `DispatcherPriority.Loaded`, which is dispatched after
  layout has run at `Render`.
- **Repaint (cheap).** Scrolling and resizing repaint from the targets already resolved; they never
  re-resolve them. Off-screen targets are skipped.
- **View-only (INV-060).** The adorner is presentation-only (`IsHitTestVisible = false`) and only ever
  draws. Nothing it holds feeds back into Capture, so seeing what changed never changes the Markdown
  Document.
- **Stale pointers.** A repaint that races a document replacement swallows the resulting
  `InvalidOperationException`: a highlight only ever describes the document it was resolved against,
  and the next reload resolves afresh.

## Why not for a Conflict

A Conflict is resolved through the [Conflict Difference](../Invariants.md) (INV-021), which shows
**both** sides so the user can judge them. A Change Highlight can only ever show the side that won,
which is the right thing for a reload — where there was no decision to make — and the wrong thing for
a Conflict. Choosing *Reload from disk* to resolve a Conflict does raise the highlight, because at
that point the decision has been made and the disk side is what is now on screen.
