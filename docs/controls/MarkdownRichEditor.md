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
| `ExpandAllFolds()` | Unfolds every Folded Section. |
| `IsFolded(Block heading)` | Whether the Section is currently Folded. |
| `Capture()` | Captures the full logical document (visible blocks with Folded bodies spliced back in) to canonical Markdown. |

Folds are driven from the UI through the `UI.Controls.MarkdownEditingCommands` routed commands
(`ToggleFold` — Ctrl+M; `ExpandAllFolds` — Ctrl+Shift+M), which the control handles via command
bindings. Folds are presentation state and are cleared whenever `Markdown` is re-Projected.

## Events

Inherits `RichTextBox` events. The control overrides `OnTextChanged` internally to drive Capture;
consumers bind to `Markdown` rather than handling text-changed directly.

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

Headings, paragraphs, bold, italic, strikethrough, and inline code round-trip today. Additional
GFM constructs (lists, task lists, links, code blocks, blockquotes, tables, thematic breaks) are
being added one tested construct at a time — see `docs/Invariants.md` (INV-004).
