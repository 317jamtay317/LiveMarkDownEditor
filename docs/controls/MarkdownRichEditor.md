# MarkdownRichEditor

The single-pane **WYSIWYG editing surface** of LiveMarkDownEditor. A custom control deriving from
`RichTextBox` that shows a Markdown Document as a formatted **Visual Document** — a heading looks
like a heading, bold is bold — so the user edits content **without ever seeing raw Markdown
syntax**, while the canonical Markdown source is exposed for binding.

- **Class:** `UI.Controls.MarkdownRichEditor`
- **Default style:** `src/UI/Controls/MarkdownRichEditor.xaml` (merged in `App.xaml`)
- **Base:** `System.Windows.Controls.RichTextBox`

This is the one place in the UI where interaction logic lives outside a ViewModel, per the project's
Control exception to the zero-code-behind rule.

## Purpose

Keep two representations of the same content in sync:

- **Project** — assigning `Markdown` parses it (GFM) and builds the Visual Document shown to the user.
- **Capture** — when the user edits the Visual Document, the control serialises it back to canonical
  Markdown and pushes it to `Markdown`.

The heavy lifting lives in `UI.Wysiwyg.MarkdownToFlowDocumentProjector` and
`UI.Wysiwyg.FlowDocumentToMarkdownCapturer`; the control orchestrates them and guards against the
two directions echoing each other.

## Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Markdown` | `string` | `""` | The canonical Markdown source text. **Binds two-way by default.** Setting it Projects a new Visual Document; editing the surface Captures back into it. |
| `IsCaretInTable` | `bool` | `false` | Whether the caret sits inside a Table — the availability switch for the Table Formatting Actions (Insert Table only outside, Add Row / Add Column only inside). |
| `FindQuery` | `string` | `""` | The Find query. Every occurrence in the Visual Document is highlighted as a Match. |
| `IsFindActive` | `bool` | `false` | Whether the Find Bar is open. Setting it false clears the Find highlights. |
| `Replacement` | `string` | `""` | The text a Match is swapped for, inserted verbatim (INV-022). |
| `IsReplaceActive` | `bool` | `false` | Whether the Find Bar's Replace Row is shown. Ctrl+H opens the Find Bar with it; Ctrl+F without. |
| `MatchCount` | `int` | `0` | **Read-only.** The number of Matches for the current `FindQuery`. |
| `MatchSummary` | `string` | `""` | **Read-only.** The Find Bar's summary: empty with no query, `"No results"`, or `"{ordinal} of {count}"`. |

## Formatting Actions (Toggle Code &amp; Tables)

Formatting Actions are real edits: they change the Visual Document, which Captures back into
`Markdown` like any other edit, always to canonical Markdown (INV-018). They are driven through
`UI.Controls.MarkdownEditingCommands` routed commands (wire a button or menu item with
`CommandTarget` aimed at the editor), or called directly:

| Member | Command | Description |
| --- | --- | --- |
| `ToggleCodeAtSelection()` | `ToggleCode` | A selection within a single line becomes a Code Span; a selection spanning multiple lines, or a whole line, becomes a Code Block; inside existing code the code formatting is removed. Enabled when text is selected or the caret is in code. |
| `InsertTableAtCaret()` | `InsertTable` | Inserts a new Table — three columns, a header row, two empty body rows — at the caret and selects the first header cell. Enabled only while the caret is **not** in a Table. |
| `AddTableRowAtCaret()` | `AddTableRow` | Inserts a new empty row below the caret's row, at the Table's column count (INV-019). Enabled only while the caret is in a Table. |
| `AddTableColumnAtCaret()` | `AddTableColumn` | Inserts a new empty column right of the caret's column, extending every row (INV-019). Enabled only while the caret is in a Table. |

The formatting logic lives in `UI.Wysiwyg.CodeFormatting` and `UI.Wysiwyg.TableEditing`, which the
Projector shares, so Capture treats user-applied code and Tables exactly like ones loaded from
Markdown.

## Folding (collapsible Sections)

The editor can **Fold** a Section — hide a heading's Section Body up to the next heading of equal or
higher level, the way Visual Studio collapses a region. Folding is **view-only**: Folded bodies are
retained and Captured in place, so a Fold never changes `Markdown` (INV-011). Section boundaries are
computed by the pure `UI.Wysiwyg.SectionMap`.

| Member | Description |
| --- | --- |
| `Fold(Block heading)` | Folds the Section led by `heading`. Throws `ArgumentException` if the block is not a Section Heading. |
| `Unfold(Block heading)` | Restores the Section's Section Body. |
| `ToggleFold(Block heading)` | Folds if Unfolded, Unfolds if Folded. |
| `ToggleFoldAtCaret()` | Toggles the Fold of the Section containing the caret. |
| `CollapseAllFolds()` | Folds every Section, collapsing the document to its top-level Section Headings. |
| `ExpandAllFolds()` | Unfolds every Folded Section. |
| `IsFolded(Block heading)` | Whether the Section is currently Folded. |
| `IsSectionHeading(Block block)` | Whether the block is a Section Heading (a foldable heading). Used by the Editor Gutter to place a Fold Toggle. |
| `Capture()` | Captures the full logical document (visible blocks with Folded bodies spliced back in) to canonical Markdown. |

Folds are driven from the UI through the `UI.Controls.MarkdownEditingCommands` routed commands
(`ToggleFold` — Ctrl+M; `CollapseAllFolds`; `ExpandAllFolds` — Ctrl+Shift+M), which the control
handles via command bindings, and through the per-heading Fold Toggle chevrons in the
[Editor Gutter](EditorGutter.md). Folds are presentation state and are cleared whenever `Markdown`
is re-Projected.

## Find &amp; Replace

**Find** locates every occurrence of `FindQuery` in the Visual Document and highlights them through
the [Find Highlight Adorner](FindHighlightAdorner.md). Find is **view-only**: it highlights, scrolls,
and selects, but never changes `Markdown` (INV-016). The scan is the pure `UI.Find.MatchScanner`,
which snapshots the document's text, delegates the search to `UI.Find.MatchFinder`, and maps the
Matches back to ranges — so a Match may span an inline formatting boundary but never bridges two
blocks.

**Replace** is the part that edits: it swaps a Match for the `Replacement` and Captures the result
back into `Markdown` like any other edit (INV-022).

| Member | Command | Description |
| --- | --- | --- |
| — | `ShowFind` | Opens the Find Bar and focuses the query box (Ctrl+F). Leaves the Replace Row hidden. |
| — | `ShowReplace` | Opens the Find Bar **with** the Replace Row (Ctrl+H). |
| — | `HideFind` | Closes the Find Bar and its Replace Row, clearing the highlights (Escape). |
| — | `FindNext` / `FindPrevious` | Move the Current Match, wrapping around the ends (F3 / Shift+F3). Enabled while there are Matches. |
| `ReplaceCurrentMatch()` | `Replace` | Swaps the Current Match for the `Replacement`, then moves to the next Match. Enabled while there are Matches — it acts on the Current Match, so it needs one. |
| `ReplaceAllMatches()` | `ReplaceAll` | Swaps every Match for the `Replacement` in one undoable edit. Enabled whenever there is a query — deliberately **not** gated on `MatchCount`, because the occurrences it exists to catch may all be hidden inside Folded Sections, leaving the count at zero. |

Four behaviours are worth knowing, all pinned by `MarkdownRichEditorReplaceTests`:

- **A Replacement is verbatim.** A Match is found case-insensitively, but the Replacement is never
  re-cased to suit it. An empty Replacement deletes the Match — the way to delete every occurrence.
- **Formatting is inherited only when the Match has one.** Replacing a word inside bold text leaves
  it bold; a Match *spanning* a formatting boundary (`**bo**ld`) has no single formatting to inherit,
  so its Replacement is plain. This holds for bold, italic, code, and strikethrough alike.
- **Replace All Unfolds first.** Find searches only the *visible* document, so an occurrence inside a
  Folded Section Body is invisible to it while still being present in `Markdown`. Replace All calls
  `ExpandAllFolds()` and re-finds before replacing, so "All" means the whole Markdown Document.
- **Replace All is one edit.** It replaces a snapshot of the ranges taken before the first edit — so
  a Replacement containing the query cannot cascade — wrapped in `BeginChange()`/`EndChange()`, which
  makes the batch a single undo unit. WPF attaches no undo stack until the control is loaded in a
  visual tree, so the undo grouping is verified by driving Ctrl+Z in the running app rather than by a
  headless test.

## Outline &amp; Navigation

The editor exposes its **Outline** — every Section Heading, in document order — so the
[Navigation Panel](OutlinePanel.md) can list them and jump between them. The Outline lists *all*
Section Headings, including ones inside a Folded Section Body, so it always mirrors the whole
document. Reading the Outline and Navigating are **view-only**: neither changes `Markdown` (INV-012).

| Member | Description |
| --- | --- |
| `Outline` | `IReadOnlyList<SectionHeading>` — every Section Heading (level + text) in document order, Folded ones included. Each `SectionHeading` is an Outline Entry. |
| `Navigate(SectionHeading heading)` | Reveals the heading (Unfolding its enclosing Section if hidden), selects it, and scrolls it into view. |
| `CurrentSection` | The `SectionHeading` whose Section most immediately encloses the caret, or `null`. |
| `OutlineChanged` (event) | Raised when the Outline may have changed (re-Projection or a structural edit). |
| `CurrentSectionChanged` (event) | Raised when the Current Section may have changed (caret move or re-Projection). |

## Events

Inherits `RichTextBox` events, plus `OutlineChanged` and `CurrentSectionChanged` (above). The control
overrides `OnTextChanged` internally to drive Capture; consumers bind to `Markdown` rather than
handling text-changed directly.

## Behaviour notes

- **Formatting is detected by effective run properties**, so both formatting loaded from Markdown
  and formatting applied via the toolbar (`EditingCommands.ToggleBold` / `ToggleItalic`, which set
  `FontWeight` / `FontStyle`) round-trip to `**` / `*`.
- **Re-entrancy guard:** an internal flag plus a "last captured" comparison stop a Capture-driven
  update to `Markdown` from re-Projecting (which would reset the caret).
- **Live external updates:** when the bound Editor Session replaces `Markdown` (e.g. the Watched
  File changed on disk), the Visual Document is re-Projected to match.

## Usage

```xml
<controls:MarkdownRichEditor
    Markdown="{Binding Markdown, UpdateSourceTrigger=PropertyChanged}" />
```

Bind `Markdown` to the Editor Session's canonical source text. Add formatting buttons that target the
editor by name:

```xml
<Button Content="B" Command="EditingCommands.ToggleBold"
        CommandTarget="{Binding ElementName=Editor}" />
```

## Supported Markdown constructs

Headings, paragraphs, bold, italic, strikethrough, Code Spans, fenced and indented Code Blocks,
Unordered and Ordered Lists (with nesting), task-list items, Links, autolinks, Images, Block
Quotes, Thematic Breaks, GFM Tables (with column alignment), and hard line breaks all round-trip —
see `docs/Invariants.md` (INV-004) for the authoritative list.
