# Ubiquitous Language

The shared vocabulary of the LiveMarkDownEditor domain. These terms are **authoritative**: every
type, method, namespace, variable, and test name must use them exactly. Do not introduce synonyms.
To add a concept, define it here first, then use it in code.

> Status: living document. Add terms as the domain grows.

## The product in one sentence

LiveMarkDownEditor is a **single-pane WYSIWYG** Markdown editor: the user reads and edits
formatted content directly (a heading looks like a heading), never raw Markdown syntax, while the
file on disk stays plain Markdown and updates live when changed by anyone — another user or an AI.

## Terms

| Term | Definition |
| --- | --- |
| **Markdown Document** | The source text authored in Markdown syntax. The primary aggregate of the editor and the single canonical representation of the content. Its source text is what is persisted to the Watched File. |
| **Visual Document** | The formatted, editable projection of a Markdown Document that the user actually sees and edits in the single WYSIWYG pane. It shows rendered formatting (bold as bold, a heading as a heading) and never exposes raw Markdown syntax. In the WPF UI it is realised as a `FlowDocument`. |
| **Project** | The act of transforming a Markdown Document's source text into a Visual Document for display and editing. (Source → Visual.) |
| **Capture** | The act of transforming the user-edited Visual Document back into Markdown Document source text. (Visual → Source.) The inverse direction of Project. |
| **Round-Trip** | A Project immediately followed by a Capture. A Round-Trip must preserve the semantic content of the Markdown Document (see Invariants). |
| **Render** | The act of transforming a Markdown Document's source text into its HTML representation. Used for export and interoperability, **not** for the on-screen editing surface (that is the Visual Document). |
| **Rendered Output** | The HTML produced by rendering a Markdown Document. |
| **Editor Session** | An active editing context holding the current Markdown Document, its Visual Document projection, the associated Watched File, and whether unsaved edits exist. |
| **Watched File** | The file on disk that backs the Editor Session. Its contents are the persisted Markdown Document source text, and it is monitored for External Change. |
| **External Change** | A modification to the Watched File made outside the Editor Session (by another user, process, or AI) while the session is open. |
| **Conflict** | The situation where an External Change is detected while the Editor Session has unsaved edits — the disk and the in-memory Markdown Document have diverged. A Conflict is never resolved by silently discarding either side; it is surfaced to the user for a decision (keep edits, reload from disk, or view the difference). |
| **Workspace** | The set of open Editor Sessions the user is working with at once, presented as Tabs, with exactly one of them the Active Session. The editor's shell: it is what lets several Markdown Documents be open and edited side by side. A Workspace is never empty — it always holds at least one Editor Session. |
| **Active Session** | The single Editor Session in the Workspace currently shown in the editing pane and targeted by the save and formatting actions. Selecting a Tab changes which Editor Session is the Active Session. |
| **Tab** | The visual handle representing one Editor Session within the Workspace. Selecting a Tab makes its Editor Session the Active Session; closing a Tab ends that Editor Session (never discarding unsaved edits without a decision — see Invariants). |

<!--
Add new terms above. Each term should have:
- a single, unambiguous definition
- no overlap or synonym with an existing term
When a term changes meaning, update every usage in code and tests in the same change.

Note: an earlier draft defined "Live Preview" as rendered output shown ALONGSIDE the source in a
split view. That model was superseded by the single-pane WYSIWYG model (Visual Document). The term
"Live Preview" is retired — do not reintroduce it.
-->
