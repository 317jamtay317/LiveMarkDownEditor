# PanelColumn

The attached behaviour that owns a [Panel Column](../UbiquitousLanguage.md)'s width — the Workspace
grid column holding a toggleable panel. While its panel is shown the column takes the width the
user last dragged its Panel Splitter to (or `VisibleWidth` until it has been dragged); while its
panel is hidden — Closed or Auto-Hidden — the column takes **no width at all**, so the Visual
Document fills the whole Workspace (INV-056, INV-062). A **fill** column instead takes the editing
area's remaining width while shown: the primary Document Pane's column — the Editor Pane's always,
and the Source Panel's exactly while the Editor Pane is not Docked (INV-063).

- **Class:** `UI.Controls.PanelColumn` — a static attached-property behaviour targeting
  `ColumnDefinition`.
- **Used by:** [`MainWindow.xaml`](../../src/UI/MainWindow.xaml) on the editing grid's Editor,
  Source, and Preview columns.

## How it works

The width cannot be projected from the visibility flag by a converter alone: dragging a
`GridSplitter` writes the column's `Width` directly, and a direct write replaces a one-way binding
— after the first drag the binding is gone, and hiding the panel would leave the dragged-open
column behind. Owning both the toggle and the remembered drag in one behaviour keeps the two from
fighting. The minimum bounds the splitter, not the toggle: a hidden column gives up its minimum
along with its width, so it truly collapses. A pixel width is remembered whenever the behaviour
replaces it (on hiding, or on becoming the fill column), so showing the panel again — or handing
the fill role back — restores the dragged width; a star width is never remembered.

## Attached properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsVisible` | `bool?` | `null` | Whether the column's panel is shown. Hidden ⇒ zero width, zero minimum. |
| `VisibleWidth` | `double` | `380` | The width the panel opens at before it has been dragged. |
| `MinimumWidth` | `double` | `180` | The narrowest a Panel Splitter may drag the shown panel. |
| `Fill` | `bool` | `false` | Fill column: shown, it takes the remaining width (star) at `MinimumWidth` instead of a remembered pixel width (INV-063). |

## Usage

```xml
<Grid.ColumnDefinitions>
    <!-- The Editor Pane: fills while Docked, collapses entirely while not (INV-063). -->
    <ColumnDefinition controls:PanelColumn.Fill="True"
                      controls:PanelColumn.MinimumWidth="240"
                      controls:PanelColumn.IsVisible="{Binding IsEditorPaneVisible}" />
    <ColumnDefinition Width="Auto" />
    <!-- The Source Panel: a dragged-width panel that fills while it is the primary Document Pane. -->
    <ColumnDefinition controls:PanelColumn.VisibleWidth="420"
                      controls:PanelColumn.Fill="{Binding IsSourcePanelPrimary}"
                      controls:PanelColumn.IsVisible="{Binding IsSourcePanelVisible}" />
</Grid.ColumnDefinitions>
```

## Styling

None — the behaviour writes layout (`Width`/`MinWidth`) only and draws nothing.
