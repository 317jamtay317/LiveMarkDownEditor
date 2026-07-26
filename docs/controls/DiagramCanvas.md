# DiagramCanvas

The **Diagram Builder**'s drag-and-drop surface: [Diagram Nodes](../UbiquitousLanguage.md) as
draggable boxes joined by drawn [Diagram Edges](../UbiquitousLanguage.md). It is the graphical half of
the Flow Charts feature — a new authoring surface over the same Mermaid source the text authoring and
[Diagram Preview](../UbiquitousLanguage.md) use. One canvas serves every node/arrow
[Diagram Kind](../UbiquitousLanguage.md): a Flowchart, a State Diagram, a Class Diagram, or an Entity
Relationship Diagram (INV-070).

- **Class:** `UI.Controls.DiagramCanvas` (derives from `System.Windows.Controls.Control`)
- **Look:** `Controls/DiagramCanvas.xaml` (an implicit style merged from the app resources)

Authored as a custom Control (interaction logic in the class, look in the ResourceDictionary), per the
project's Control exception to the zero-code-behind rule — a drag-and-drop canvas is imperative, so the
control drives it in code. It holds no diagram state of its own: every gesture becomes a call on the
bound [`DiagramBuilderViewModel`](../../src/UI/ViewModels/DiagramBuilderViewModel.cs), and the node
positions it sets are builder view state, **never** emitted to Mermaid (Mermaid computes layout —
INV-051).

## How it works

The control's template stacks two `ItemsControl`s over a `Canvas` panel: the **edges** layer beneath
and the **nodes** layer on top, plus a rubber-band `Line` (`PART_RubberBand`) for the connect gesture.
Each node is positioned by binding `Canvas.Left`/`Canvas.Top` to its `X`/`Y`; each edge draws itself
between its endpoints' centres (a `MultiBinding` through `EdgeGeometryConverter`), so edges follow
nodes as they move. A node's shape comes from `NodeShapeGeometryConverter`; an edge's thickness and
dashes from `EdgeThicknessConverter` / `EdgeDashConverter`.

### Drawing each Diagram Kind

`DiagramEdgeGeometry` builds an edge's shaft plus the end markers its `EdgeKind` calls for — an
arrowhead, a class relationship's triangle or diamond, or an Entity Relationship cardinality's bar or
crow's foot. Because some markers are **hollow**, the edge template draws two `Path`s from the same
converter: the solid layer (stroked and filled in the edge's own brush) and, over it, the
`ConverterParameter="Hollow"` layer filled with the canvas background — which is what tells
Aggregation from Composition and Inheritance from a plain association. A class relationship's marker
sits at the **From** end, matching Mermaid's reading of `Animal <|-- Duck`.

A State Diagram's **Terminal** is drawn as a filled circle rather than a box: a `DataTrigger` on
`NodeShape.Terminal` fills the shape with the foreground brush, hides the Node Label, and moves the
connector handle in to the circle's edge. The pickers in the dialog spell their terms as words through
`PascalCaseWordsConverter` ("One to many", "Entity relationship diagram").

Interaction is handled in the control class:

- **Move** — left-drag a node's body updates its `X`/`Y` through `DiagramBuilderViewModel.MoveNode`.
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
| `Builder` | `DiagramBuilderViewModel?` | The builder whose Diagram Nodes and Edges the canvas presents and edits. Bound to the dialog's DataContext. |

## Usage

```xml
<controls:DiagramCanvas Builder="{Binding}" />
```
