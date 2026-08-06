# CodeShadingAdorner

The **CodeShadingAdorner** draws the Code Shading over the
[MarkdownRichEditor](MarkdownRichEditor.md)'s Visual Document: a subtle shaded panel behind every
Code Block and Code Span, so code is set off from prose. It is the visible half of Code Shading; the
finding of the Code Regions is the pure [`CodeShadingScanner`](../../src/UI/Wysiwyg/CodeShadingScanner.cs).

- **Class:** `UI.Controls.CodeShadingAdorner` (derives from `System.Windows.Documents.Adorner`)
- **Attached by:** `MarkdownRichEditor` on `Loaded`, into the editor's `AdornerLayer`.

## Why an overlay instead of a background

Code used to carry its own `TextElement.Background` (a palette brush). That looked right, but it was
expensive: recoloring a brush that is a text element's background forces WPF to **re-format** every
line of text using it. On a theme switch that re-formatted the whole document — hundreds of
milliseconds on a code-heavy document.

Drawing the shade as an adorner instead makes it a plain filled rectangle in an overlay that owns no
text. Recoloring it (a theme switch) only repaints the overlay — no document reflow. So the shading
recolors in well under a millisecond regardless of document size.

## Division of labour

Code Shading is split so that only the editor Control touches the document:

1. **The scanner finds.** `CodeShadingScanner.Scan` walks the Visual Document (descending through
   sections, lists, tables, and inline spans) and returns the ordered **Code Regions** — one per Code
   Block and one per Code Span, each flagged as block or span.
2. **The adorner draws.** The editor rebuilds the regions after each edit and hands them to the
   adorner; `OnRender` paints a rounded shade behind each on-screen region — a panel spanning the
   text column for a Code Block, a snug box hugging the text for an inline Code Span. It fills with
   the `CodeShadingBrush` looked up from the active palette, so the shade follows the light/dark theme.

## What an inline Code Span's shade costs

Nearly every Code Span sits on one visual line, and such a span is described entirely by its two ends:
[`CodeSpanShade.Box`](../../src/UI/Wysiwyg/CodeSpanShade.cs) turns the start and end character
rectangles into the box to draw, so a repaint resolves **two** layout positions for a span however many
characters it holds. It also does the Viewport cull on those two rectangles, before anything else is
computed — culling a span only once its every character has been measured is not culling at all, which
is the first rule [INV-074](../Invariants.md#inv-074) states.

The per-character walk survives only as the fallback for a span that genuinely **wraps** across a line
end, where no single box covers it and one box per visual line is the point. Previously that walk ran
for *every* span: a `GetCharacterRect` at every insertion position, each forcing WPF to resolve text
layout, so a screen of code-heavy prose cost hundreds of layout resolutions per repaint.

`Box` orders the two edges with `Min`/`Max` rather than assuming the end lies right of the start. A
degenerate or bidi-reversed span can put it on the left, and a negative width throws inside `Rect`'s
constructor — an exception raised during a render takes the app down.

## Where a Code Block's panel ends

A Code Block's panel spans the **text column**, not the control. Its left edge needs no help — the
first character sits one block-padding inside the block's left edge, so subtracting that padding
lands on the column. The right edge has no such landmark, and taking the control's width instead
overhung the column by whatever padding the surface carries: 11px in the ordinary view, and a full
inch in Page View, where the control's padding *is* the Page's Margins. The panel ran to the paper's
edge on the right while sitting an inch clear of it on the left.

[`EditorTextColumn`](../../src/UI/Controls/EditorTextColumn.cs) computes the right edge from the
hosting `ScrollViewer`'s **viewport**, which is already net of both the control's padding and the
vertical scrollbar — so the panel is evenly inset and never runs under the scrollbar. Measured on the
real control, an 816px-wide Page View surface with 96px margins gives a panel from 102 to 713, i.e.
102 clear on the left and 103 on the right. The [`ChangeHighlightAdorner`](ChangeHighlightAdorner.md)
shares the same helper, since it shades whole Blocks the same way (INV-060).

## How it works

- **Recolor is free (the whole point).** The shade is filled with the live `CodeShadingBrush`
  resource. When the theme changes, that brush's color changes and WPF re-rasterises the overlay's
  rectangles — it never re-invokes layout, so no document reflow occurs (INV-017).
- **Repaint (cheap).** Scrolling and resizing only repaint from the regions already held; the
  document is re-scanned only when it actually changes (an edit or a re-projection). Off-screen
  regions are skipped.
- **Translucent by design.** The text is drawn by the editor beneath the overlay, so the shade is a
  translucent tint the text reads through, not an opaque fill.
- **View-only (INV-017).** The adorner is presentation-only (`IsHitTestVisible = false`) and only
  ever draws. Neither it nor the Code Regions feed back into Capture, so shading code never changes
  the Markdown Document.
- **Stale pointers.** When the document is replaced the editor rebuilds the regions; a repaint that
  races that replacement swallows the resulting `InvalidOperationException` and waits for the fresh
  regions.
