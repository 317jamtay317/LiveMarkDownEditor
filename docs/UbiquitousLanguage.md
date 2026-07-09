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
| **Workspace** | The set of open Editor Sessions the user is working with at once, presented as Tabs, with exactly one of them the Active Session whenever any are open. The editor's shell: it is what lets several Markdown Documents be open and edited side by side. A Workspace opens with a single empty Editor Session, but it **may become empty**: closing the last Tab leaves it with no open Editor Session and no Active Session, showing an Empty-Workspace Placeholder until the user opens or creates a document. |
| **Active Session** | The single Editor Session in the Workspace currently shown in the editing pane and targeted by the save and formatting actions. Selecting a Tab changes which Editor Session is the Active Session. There is **no** Active Session when the Workspace is empty. |
| **Empty-Workspace Placeholder** | The message shown in the editing area when the Workspace has no open Editor Session (every Tab has been closed): a prompt inviting the user to open or create a document. Presentation-only. |
| **Tab** | The visual handle representing one Editor Session within the Workspace. Selecting a Tab makes its Editor Session the Active Session; closing a Tab ends that Editor Session (never discarding unsaved edits without a decision — see Invariants). |
| **Section** | A heading together with all Visual Document content that follows it, up to (but not including) the next heading of equal or higher level. A Section's heading is its **Section Heading**; the content it owns is its **Section Body**. Sections nest: a Section Body may contain lower-level Sections. |
| **Fold** | A view-only operation on the Visual Document that hides a Section's Section Body while leaving its Section Heading visible, the way Visual Studio collapses a method or region. The inverse is **Unfold**. Fold state is presentation-only: it is never part of the Markdown Document and never changes its source text or the result of a Capture. |
| **Collapse All / Expand All** | The two Workspace-level fold actions: **Collapse All** Folds every Section; **Expand All** Unfolds every Folded Section. Like any Fold, they are view-only and never change the Markdown Document. |
| **Editor Gutter** | The margin strip along the left edge of the WYSIWYG editing surface. It is pure presentation — it shows a **Line Number** for each visible line and a **Fold Toggle** beside each Section Heading — and is never part of the Markdown Document. |
| **Line Number** | The 1-based ordinal of a visible line of the Visual Document — one per rendered line, counting each soft-wrapped continuation as its own line the way a code editor does — shown in the Editor Gutter. Line Numbers are presentation-only; the lines of a Folded (hidden) Section Body are not numbered. |
| **Fold Toggle** | The chevron shown in the Editor Gutter beside a Section Heading — pointing down when the Section is Unfolded, right when it is Folded. Activating a Fold Toggle Folds or Unfolds that Section. |
| **Outline** | The ordered list of every Section Heading in the Active Session's Visual Document, in document order, each with its heading level and text. It lists *all* Section Headings, including those inside a Folded Section Body. The Outline is a view-only projection of the Visual Document — building it never changes the Markdown Document. |
| **Outline Entry** | One Section Heading as it appears in the Outline: a navigation target showing the heading's text, indented by its heading level so the document's structure is visible at a glance. Activating an Outline Entry Navigates to its Section Heading. An Outline Entry that has Sections nested within it (Outline Entries of lower level immediately following it) can be **Collapsed** in the Navigation Panel to hide those nested Outline Entries, or **Expanded** to show them again. This disclosure is Navigation-Panel-only: it changes neither the Markdown Document nor any Fold state in the Visual Document, and is distinct from a Fold (which hides a Section Body in the editing surface, not Outline Entries in the panel). |
| **Navigate** | The act of moving the editing surface to a Section Heading and selecting it, in response to activating its Outline Entry. If the Section Heading is inside a Folded Section Body, the enclosing Section is first Unfolded so the heading is revealed. Navigation is view-only: it selects and scrolls, but never changes the Markdown Document. |
| **Navigation Panel** | The toggleable panel along the left edge of the Workspace that presents the Active Session's Outline for Navigation. It is presentation-only — showing, hiding, or Navigating from it never changes any Markdown Document — and is hidden until the user toggles it on. |
| **Current Section** | The Section whose Section Heading most immediately precedes the caret in the Visual Document — the Section the user is currently editing within. The Navigation Panel highlights the Current Section's Outline Entry so the user can see where they are as they edit or scroll. |
| **Source Panel** | The toggleable panel that shows the Active Session's Markdown Document as raw, editable Markdown **source text** — the plain-text counterpart to the Visual Document, shown alongside it. It is the one surface that *deliberately* exposes Markdown syntax (a heading shows as `# Title`), where the Visual Document never does. It is not a separate Render: it displays the Markdown Document's own source text. Editing the Source Panel edits that source directly, which **Projects** an updated Visual Document; editing the Visual Document **Captures** back into the source, which the Source Panel reflects. The two are always kept in sync — they are two views of the same Markdown Document (see Invariants). Like the Navigation Panel it is presentation-only chrome and is hidden until the user toggles it on. |

<!--
Add new terms above. Each term should have:
- a single, unambiguous definition
- no overlap or synonym with an existing term
When a term changes meaning, update every usage in code and tests in the same change.

Note: an earlier draft defined "Live Preview" as *rendered output* (HTML) shown ALONGSIDE the
source in a split view. That model was superseded by the WYSIWYG model (Visual Document), and the
term "Live Preview" is retired — do not reintroduce it. The **Source Panel** is a distinct concept
and does NOT un-retire it: it shows the Markdown Document's own editable *source text*, not rendered
HTML, and reuses the existing Project/Capture Round-Trip rather than a separate Render. The Visual
Document remains the primary editing surface (shown by default); the Source Panel is optional chrome
the user toggles on beside it.
-->
