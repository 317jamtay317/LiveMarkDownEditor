# Invariants

The rules that must **always** hold true in the LiveMarkDownEditor domain. These are
**authoritative**: each invariant must be enforced inside the domain model and covered by at least
one xUnit + Shouldly test.

Use the terms defined in [UbiquitousLanguage.md](UbiquitousLanguage.md).

> Status: living document. Add invariants as the domain grows. Add the rule here first, then write
> a failing test, then implement.

## Format

Each invariant has a stable identifier (`INV-###`), a statement, and a note on how it is enforced
and tested.

## Invariants

### INV-001 — A Markdown Document always has non-null source text
- **Statement:** A Markdown Document's source text is never `null`. An empty document is represented
  by an empty string, not `null`.
- **Enforced by:** Constructor guard on the Markdown Document.
- **Tested by:** `MarkdownSourceTests.Construct_GivenNullText_ThrowsAndPreservesInvariant`,
  `MarkdownDocumentTests.Construct_GivenNullSource_ThrowsAndPreservesInvariant`.

### INV-002 — Rendering is deterministic
- **Statement:** Rendering the same Markdown Document source text always produces the same Rendered
  Output.
- **Enforced by:** The `IMarkdownRenderer` port contract; the Markdig adapter is a pure function
  of the source text over a fixed pipeline with no per-render state.
- **Tested by:** `MarkdigMarkdownRendererTests.Render_GivenSameSourceTwice_ProducesIdenticalOutput_INV002`.

### INV-003 — Projecting is deterministic
- **Statement:** Projecting the same Markdown Document source text always produces the same Visual
  Document. Project has no hidden state.
- **Enforced by:** Pure Project operation driven solely by the source text
  (`MarkdownToFlowDocumentProjector`).
- **Tested by:** `WysiwygRoundTripTests.RoundTrip_IsIdempotent_INV005` (a deterministic Project is a
  precondition of the stable Round-Trip). _(Dedicated projection-determinism test to be added as
  more constructs land.)_

### INV-004 — A Round-Trip preserves semantic content
- **Statement:** For any supported Markdown Document, Capturing its Projected Visual Document yields
  source text that is semantically equal to the original — it renders to the same Rendered Output.
  Formatting is expressed through the Visual Document, never as literal Markdown syntax the user can
  see. (Fidelity is guaranteed only for the currently supported set of Markdown constructs; support
  grows one tested construct at a time.)
- **Enforced by:** Project and Capture being inverse transformations over the supported constructs.
- **Tested by:** `WysiwygRoundTripTests.RoundTrip_PreservesSemantics_INV004` (verified against the
  `MarkdigMarkdownRenderer` HTML oracle). Currently supported: headings, paragraphs, bold, italic,
  strikethrough, inline code.

### INV-005 — Capture is idempotent over Round-Trips
- **Statement:** Once a Markdown Document has been Round-Tripped, Round-Tripping the result again
  produces identical source text. Capture converges — repeated Round-Trips never keep mutating the
  document (no whitespace drift, no escaping churn).
- **Enforced by:** Capture emitting normalised, canonical Markdown (`FlowDocumentToMarkdownCapturer`).
- **Tested by:** `WysiwygRoundTripTests.RoundTrip_IsIdempotent_INV005`.

### INV-006 — A Conflict never silently discards either side
- **Statement:** When an External Change to the Watched File is detected while the Editor Session
  has unsaved edits, a Conflict is raised and surfaced to the user for a decision. Neither the
  on-disk contents nor the unsaved edits are overwritten without an explicit choice.
- **Enforced by:** `ExternalChangeReconciler.Reconcile` and the Editor Session's
  `HandleExternalChangeAsync`, which raise a Conflict (rather than applying disk contents) when
  unsaved edits exist, and never overwrite without an explicit Keep/Reload choice.
- **Tested by:** `ExternalChangeReconcilerTests.Reconcile_WithUnsavedEdits_RaisesConflict_INV006`,
  `EditorSessionViewModelTests.ExternalChange_WithUnsavedEdits_RaisesConflict_AndKeepsEdits_INV006`.

### INV-007 — With no unsaved edits, an External Change reloads live
- **Statement:** When the Watched File changes and the Editor Session has **no** unsaved edits, the
  Markdown Document (and therefore the Visual Document) is updated to the new on-disk contents
  without prompting. This is the "live update by any user, including AI" behaviour.
- **Enforced by:** External-change reconciliation applying disk contents directly when the session
  is clean.
- **Tested by:** `ExternalChangeReconcilerTests.Reconcile_WithNoUnsavedEdits_ReloadsFromDisk_INV007`,
  `EditorSessionViewModelTests.ExternalChange_WhenSessionClean_ReloadsLive_INV007`.

### INV-008 — A Workspace always has at least one Editor Session
- **Statement:** A Workspace is never empty and always has exactly one Active Session. It opens with
  one empty Editor Session, and closing the last remaining Editor Session immediately opens a fresh
  empty one so there is always a Tab to edit in.
- **Enforced by:** `WorkspaceViewModel` — it creates an initial Editor Session in its constructor,
  keeps `ActiveSession` non-null, and re-seeds an empty session when the last Tab is closed.
- **Tested by:** `WorkspaceViewModelTests.Constructor_StartsWithOneEmptyActiveSession_INV008`,
  `WorkspaceViewModelTests.Close_LastTab_OpensFreshEmpty_INV008`.

### INV-009 — A Watched File is open in at most one Editor Session
- **Statement:** The same Watched File is never open in two Tabs at once. Opening a file that is
  already open in the Workspace activates its existing Editor Session rather than creating a
  duplicate Tab.
- **Enforced by:** `WorkspaceViewModel.Open`, which matches the chosen path against the open Editor
  Sessions' Watched Files and activates the match instead of loading a second copy.
- **Tested by:** `WorkspaceViewModelTests.Open_WhenFileAlreadyOpen_ActivatesExistingTab_INV009`.

### INV-010 — Closing an Editor Session never silently discards unsaved edits
- **Statement:** When an Editor Session with unsaved edits is closed, the user is asked to Save,
  Discard, or Cancel. Cancel aborts the close and keeps the Tab; Save persists before closing;
  Discard closes without saving. Unsaved edits are never dropped without an explicit choice. (This
  is the close-time counterpart of INV-006.)
- **Enforced by:** `WorkspaceViewModel.CloseSession` via the `IUnsavedEditsPrompt` port.
- **Tested by:** `WorkspaceViewModelTests.Close_WithUnsavedEdits_Save_Persists_INV010`,
  `WorkspaceViewModelTests.Close_WithUnsavedEdits_Discard_Closes_INV010`,
  `WorkspaceViewModelTests.Close_WithUnsavedEdits_Cancel_KeepsTab_INV010`.

<!--
Add new invariants above using the next INV-### number. Never reuse a retired number.
Every invariant MUST have at least one corresponding test before it is considered done.
-->
