# MermaidDiagramView

The inline picture of a [Mermaid Diagram](../UbiquitousLanguage.md) in the Visual Document. A Mermaid
Diagram is shown in the editing surface as its **rendered image**, not as source code (INV-047); this
control is that image.

- **Class:** `UI.Controls.MermaidDiagramView` (derives from `System.Windows.Controls.ContentControl`)
- **Hosted in:** a `BlockUIContainer` tagged with a `MermaidDiagramRole` (which carries the source, so
  Capture re-emits the fenced ```` ```mermaid ```` block)

Authored as a custom Control that builds its content in code, per the project's Control exception to the
zero-code-behind rule — the same pattern as [MermaidPreview](MermaidPreview.md). It holds no diagram
state that Capture reads: the source lives on the host container's `MermaidDiagramRole`, so the picture
is purely presentation and rendering it never changes the Markdown Document (INV-047).

## How it works

The [MarkdownToFlowDocumentProjector](../../src/UI/Wysiwyg/MarkdownToFlowDocumentProjector.cs) projects
a `mermaid` fenced Code Block as a `BlockUIContainer` hosting a `MermaidDiagramView` with the diagram's
`Source` — a **pure, synchronous** projection (INV-003); no rendering happens here.

After the projection, the editor's `MermaidRenderCoordinator` renders each diagram through the
`IMermaidImageRenderer` port (the WebView2-backed renderer) and sets the control's `Rendered` image. The
picture arrives asynchronously and changes no structure, exactly as a remote Image's pixels do (INV-003).
Rendered pictures are cached by source, so typing elsewhere never re-renders an unchanged diagram.

Until the picture arrives — or if the diagram cannot be rendered (no renderer, or source Mermaid
rejects) — the control shows the diagram's **source text** in a code-like box, so a diagram is never an
empty hole (the Image fallback of INV-031, reached for a diagram).

Double-clicking the picture opens the [Flowchart Builder](../UbiquitousLanguage.md) on that diagram
(handled by the editor); the [Source Panel](../UbiquitousLanguage.md) edits the raw source.

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `Source` | `string?` | The Mermaid Diagram source the picture renders (and the builder edits). |
| `Rendered` | `ImageSource?` | The rendered picture, supplied by the coordinator; `null` shows the source-text fallback. |
