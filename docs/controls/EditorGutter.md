# EditorGutter

The **Editor Gutter**: the presentation-only margin strip drawn to the left of the
[MarkdownRichEditor](MarkdownRichEditor.md). It shows a **Line Number** for each visible content line
of the Visual Document and a **Fold Toggle** (a chevron) beside each Section Heading that Folds or
Unfolds that Section on click.

- **Class:** `UI.Controls.EditorGutter` (derives from `System.Windows.Controls.Canvas`)
- **Default style:** `src/UI/Controls/EditorGutter.xaml` (merged in `App.xaml`)

Authored as a custom Control (class plus a ResourceDictionary for its look), per the project's
Control exception to the zero-code-behind rule. The gutter is **view-only**: it reads the editor's
blocks, fold state, and line layout and mirrors them, and never mutates the document — so it cannot
change the Markdown source (INV-011).

## How it works

The gutter positions its glyphs from `TextPointer.GetCharacterRect`, which reports each line's
position relative to the editor's viewport. It re-lays out whenever the editor's text, size, or
scroll offset changes (via `TextChanged`, `SizeChanged`, and the bubbled
`ScrollViewer.ScrollChangedEvent`), so numbers and chevrons stay aligned as the user types and
scrolls.

- **Line Numbers** — one per visible rendered line, counting each soft-wrapped continuation as its
  own line (as a code editor does), numbered in document order. The walk follows the editor's
  rendered lines via `TextPointer.GetLineStartPosition`. The lines of a Folded Section Body are
  hidden and therefore not numbered, so the numbering reflects what is currently visible.
- **Fold Toggles** — a chevron beside each Section Heading: **▾** (down) when the Section is
  Unfolded, **▸** (right) when it is Folded. Clicking it calls `MarkdownRichEditor.ToggleFold` for
  that heading. Document-wide folding is on the command bar as **Collapse all** / **Expand all**.

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `Editor` | `MarkdownRichEditor?` | The editor whose lines and Sections this gutter mirrors. Setting it wires the refresh subscriptions. |
| `LineNumberBrush` | `Brush` | Brush for Line Numbers. Themed to `MutedTextBrush`. |
| `ChevronBrush` | `Brush` | Brush for a Fold Toggle chevron. Themed to `MutedTextBrush`. |
| `ChevronHoverBrush` | `Brush` | Brush for a Fold Toggle chevron while hovered. Themed to `AccentBrush`. |

## Usage

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <controls:EditorGutter Grid.Column="0" Editor="{Binding ElementName=Editor}" />
    <controls:MarkdownRichEditor x:Name="Editor" Grid.Column="1"
                                 Markdown="{Binding ActiveSession.Markdown, UpdateSourceTrigger=PropertyChanged}" />
</Grid>
```
