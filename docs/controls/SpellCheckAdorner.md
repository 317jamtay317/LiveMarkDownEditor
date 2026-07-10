# SpellCheckAdorner

The **SpellCheckAdorner** draws red squiggles under the misspelled words of the
[MarkdownRichEditor](MarkdownRichEditor.md)'s Visual Document. It replaces WPF's built-in spell
check, which cannot segment code-like identifiers and so flags the whole of
`this.ShouldBe().Invld()` instead of only the genuinely misspelled part.

- **Class:** `UI.Controls.SpellCheckAdorner` (derives from `System.Windows.Documents.Adorner`)
- **Attached by:** `MarkdownRichEditor` on `Loaded`, into the editor's `AdornerLayer`.

## Why a custom checker

WPF's `SpellCheck.IsEnabled` is control-level and offers no per-run opt-out and no control over word
segmentation. Two things follow that it cannot do:

1. **Exclude code.** Handled at projection time — code runs carry the language tag `zxx` (no
   linguistic content); this adorner skips any run so tagged.
2. **Segment identifiers.** `this.ShouldBe().Invld()` must break into `this` / `Should` / `Be` /
   `Invld` so each is judged on its own and only `Invld` is flagged. That segmentation is the
   camelCase- and punctuation-aware [`WordTokenizer`](../../src/UI/Spelling/WordTokenizer.cs); the
   dictionary lookups come from [`SpellCheckScanner`](../../src/UI/Spelling/SpellCheckScanner.cs)
   over an `ISpellDictionary` (the OS speller, via `WindowsSpellDictionary`).

## How it works

- **Scan (debounced).** On `TextChanged` the adorner clears its ranges, then — once edits settle —
  walks the document's prose runs (into sections, lists, tables, and spans), runs the scanner over
  each, and stores a `TextRange` per misspelling. The dictionary pass runs only here.
- **Paint (cheap).** `OnRender` draws a squiggle under each stored range that is on-screen. Scrolling
  and resizing only repaint from the existing ranges — they never re-run the dictionary, so scrolling
  stays cheap.
- **Graceful degradation.** If the OS speller is unavailable, `WindowsSpellDictionary` reports every
  word as correct, so no squiggles appear and nothing fails.

## Spelling Suggestions

The adorner does not paint the squiggles interactively (`IsHitTestVisible = false`), but it does own
the authoritative list of Misspelling ranges, so it answers one query for the editor:
`MisspellingAt(TextPointer)` returns the Misspelling under a point, or `null`. On a right-click the
[MarkdownRichEditor](MarkdownRichEditor.md) uses it to decide whether to head its context menu with
Spelling Suggestions — `SpellingSuggestions.For(word, dictionary)` over the same `ISpellDictionary`.
Choosing a suggestion replaces the Misspelling's span, which Captures back into the Markdown source
like any other edit. Painting itself never changes the source.
