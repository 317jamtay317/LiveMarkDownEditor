# OutlinePanel

The **Navigation Panel**: the presentation-only panel along the left edge of the Workspace that lists
the Active Session's **Outline** — every **Section Heading** as a clickable **Outline Entry**,
indented by heading level. It is presented as a tab of the [Side Dock](../UbiquitousLanguage.md)
(alongside the Folder Panel, so the two share one column) and is hidden until the user toggles it on
(`SideDockViewModel.IsNavigationTabVisible`, INV-046).

- **Class:** `UI.Controls.OutlinePanel` (derives from `System.Windows.Controls.ListBox`)
- **Default style:** `src/UI/Controls/OutlinePanel.xaml` (merged in `App.xaml`)

Authored as a custom Control (a `ListBox` subclass plus a ResourceDictionary for its look), per the
project's Control exception to the zero-code-behind rule — the same pattern as the
[EditorGutter](EditorGutter.md). The panel is **view-only**: it reads the editor's Outline and drives
Navigation, and never mutates the document — so it cannot change the Markdown source (INV-012).

## How it works

The panel mirrors the editor through its `Editor` dependency property (bound the same way as the
gutter):

- **Listing** — on [`MarkdownRichEditor.OutlineChanged`](MarkdownRichEditor.md) it sets its
  `ItemsSource` to `MarkdownRichEditor.Outline`, the ordered list of every Section Heading (including
  ones inside a Folded Section Body, so the whole document is always represented). Each Outline Entry
  is indented by `HeadingLevelToIndentConverter` so subsections nest under their parents.
- **Navigating** — selecting an Outline Entry (a click) calls `MarkdownRichEditor.Navigate`, which
  reveals the heading (Unfolding its enclosing Section if hidden), selects it, and scrolls it into
  view.
- **Collapse / Expand** — an Outline Entry that leads nested entries shows a chevron (▾ Expanded, ▸
  Collapsed). Clicking it Collapses or Expands that entry, hiding or showing the nested Outline
  Entries beneath it. Which entries are hidden under a Collapsed ancestor is computed by the pure
  `UI.Wysiwyg.OutlineView`. Collapse state is panel-only (it never changes the document or any Fold —
  INV-012) and is preserved across edits, keyed by heading block. The chevron is a `Button`, so
  clicking it toggles the Collapse without selecting the row or Navigating.
- **Highlighting the Current Section** — on `MarkdownRichEditor.CurrentSectionChanged` it selects the
  Outline Entry of `MarkdownRichEditor.CurrentSection`, so the entry for the Section under the caret
  stays highlighted as the user edits and scrolls. These self-made selections are guarded so they
  never trigger Navigation back into the editor.

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `Editor` | `MarkdownRichEditor?` | The editor whose Outline this panel lists and Navigates. Setting it wires the Outline/Current-Section subscriptions. |

## Usage

```xml
<!-- Hosted in the Side Dock's Outline tab, shown while that tab is the Selected one. -->
<controls:OutlinePanel Editor="{Binding ElementName=Editor}" />
...
<controls:MarkdownRichEditor x:Name="Editor"
                             Markdown="{Binding ActiveSession.Markdown, UpdateSourceTrigger=PropertyChanged}" />
```
