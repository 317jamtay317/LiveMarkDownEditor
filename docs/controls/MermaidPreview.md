# MermaidPreview

The **Preview Panel**'s rendering surface: a [Diagram Preview](../UbiquitousLanguage.md) of the
[Mermaid Diagram](../UbiquitousLanguage.md) the caret is currently within, rendered live by the
bundled Mermaid library running in a WebView2.

- **Class:** `UI.Controls.MermaidPreview` (derives from `System.Windows.Controls.ContentControl`)
- **Hosts:** `Microsoft.Web.WebView2.Wpf.WebView2` as its content

Authored as a custom Control (interaction logic in the class), per the project's Control exception to
the zero-code-behind rule — a WebView2 is imperative, so the control creates and drives it in code.
Rendering is **view-only**: the control reads `DiagramSource` and draws it, and never mutates any
Markdown Document (INV-047). It is shown in the [Preview Panel](../UbiquitousLanguage.md), toggled
from the Command Bar (INV-048).

## How it works

On `Loaded`, the control initialises the WebView2 and maps a virtual host (`mermaid.host`) to the
app's bundled Mermaid assets folder (`Assets/Mermaid`), then navigates to the bundled host page
`index.html`. The page carries the version-pinned Mermaid library, so **no network is used** — the
Diagram Preview works entirely offline.

When `DiagramSource` or `IsDark` changes (and once the host page has loaded), the control calls the
page's `renderDiagram(source, dark)` via `ExecuteScriptAsync`, which renders the diagram and injects
its SVG. A `null`/empty `DiagramSource` shows a hint ("place the caret in a mermaid diagram"); a
diagram Mermaid rejects shows the parser's error message rather than a blank pane.

If the WebView2 runtime is unavailable, initialisation fails softly and the pane stays blank — the
rest of the app is unaffected, because rendering is view-only.

The same bundled host page is reused by the PDF image renderer
(`UI.Platform.WebView2MermaidImageRenderer`), which drives it off-screen to rasterise each diagram
for an Export as PDF (INV-050).

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `DiagramSource` | `string?` | The Mermaid Diagram source to render, or `null` when the caret is not in a Mermaid Diagram. Bound to the editor's read-only `CurrentDiagramSource` (INV-047). |
| `IsDark` | `bool` | Whether the active theme is dark, so the Diagram Preview matches the editor's light/dark palette. |

## Usage

```xml
<controls:MermaidPreview DiagramSource="{Binding CurrentDiagramSource, ElementName=Editor}"
                         IsDark="{Binding Appearance.IsDarkTheme}" />
```
