# FlowchartCanvas

The **Flowchart Builder**'s drag-and-drop surface: [Diagram Nodes](../UbiquitousLanguage.md) as
draggable boxes joined by drawn [Diagram Edges](../UbiquitousLanguage.md). It is the graphical half of
the Flow Charts feature — a new authoring surface over the same Mermaid source the text authoring and
[Diagram Preview](../UbiquitousLanguage.md) use.

- **Class:** `UI.Controls.FlowchartCanvas` (derives from `System.Windows.Controls.Control`)
- **Look:** `Controls/FlowchartCanvas.xaml` (an implicit style merged from the app resources)

Authored as a custom Control (interaction logic in the class, look in the ResourceDictionary), per the
project's Control exception to the zero-code-behind rule — a drag-and-drop canvas is imperative, so the
control drives it in code. It holds no diagram state of its own: every gesture becomes a call on the
bound [`FlowchartBuilderViewModel`](../../src/UI/ViewModels/FlowchartBuilderViewModel.cs), and the node
positions it sets are builder view state, **never** emitted to Mermaid (Mermaid computes layout —
INV-051).

## How it works

The control's template stacks two `ItemsControl`s over a `Canvas` panel: the **edges** layer beneath
and the **nodes** layer on top, plus a rubber-band `Line` (`PART_RubberBand`) for the connect gesture.
Each node is positioned by binding `Canvas.Left`/`Canvas.Top` to its `X`/`Y`; each edge draws itself
between its endpoints' centres (a `MultiBinding` through `EdgeGeometryConverter`), so edges follow
nodes as they move. A node's shape comes from `NodeShapeGeometryConverter`; an edge's thickness and
dashes from `EdgeThicknessConverter` / `EdgeDashConverter`.

Interaction is handled in the control class:

- **Move** — left-drag a node's body updates its `X`/`Y` through `FlowchartBuilderViewModel.MoveNode`.
- **Connect** — left-drag from a node's **connector handle** (the accent dot, tagged `Connector`) draws
  a rubber-band line; on release the control **hit-tests the drop point** (not `e.OriginalSource`,
  which is the capturing canvas while the mouse is captured) to find the target node, then calls
  `Connect`.
- **Select** — a click selects the node or edge under the cursor (or clears the selection on empty
  canvas); the selection is highlighted and is the Delete target.
- **Rename** — a double-click on a node begins an inline rename (`IsEditing`), committed on Enter or
  Escape.

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `Builder` | `FlowchartBuilderViewModel?` | The builder whose Diagram Nodes and Edges the canvas presents and edits. Bound to the dialog's DataContext. |

## Usage

```xml
<controls:FlowchartCanvas Builder="{Binding}" />
```
