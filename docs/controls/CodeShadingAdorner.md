# CodeShadingAdorner

The **CodeShadingAdorner** draws the Code Shading over the
[MarkdownRichEditor](MarkdownRichEditor.md)'s Visual Document: a subtle shaded panel behind every
Code Block and Code Span, so code is set off from prose. It is the visible half of Code Shading; the
finding of the Code Regions is the pure [`CodeShadingScanner`](../../src/UI/Wysiwyg/CodeShadingScanner.cs).

- **Class:** `UI.Controls.CodeShadingAdorner` (derives from `System.Windows.Documents.Adorner`)
- **Attached by:** `MarkdownRichEditor` on `Loaded`, into the editor's `AdornerLayer`.

## Why an overlay instead of a background

Code used to carry its own `TextElement.Background` (a palette brush). That looked right, but it was
expensive: recolouring a brush that is a text element's background forces WPF to **re-format** every
line of text using it. On a theme switch that re-formatted the whole document — hundreds of
milliseconds on a code-heavy document.

Drawing the shade as an adorner instead makes it a plain filled rectangle in an overlay that owns no
text. Recolouring it (a theme switch) only repaints the overlay — no document reflow. So the shading
recolours in well under a millisecond regardless of document size.

## Division of labour

Code Shading is split so that only the editor Control touches the document:

1. **The scanner finds.** `CodeShadingScanner.Scan` walks the Visual Document (descending through
   sections, lists, tables, and inline spans) and returns the ordered **Code Regions** — one per Code
   Block and one per Code Span, each flagged as block or span.
2. **The adorner draws.** The editor rebuilds the regions after each edit and hands them to the
   adorner; `OnRender` paints a rounded shade behind each on-screen region — a full-width panel for a
   Code Block, a snug box hugging the text for an inline Code Span. It fills with the
   `CodeShadingBrush` looked up from the active palette, so the shade follows the light/dark theme.

## How it works

- **Recolour is free (the whole point).** The shade is filled with the live `CodeShadingBrush`
  resource. When the theme changes, that brush's colour changes and WPF re-rasterises the overlay's
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
