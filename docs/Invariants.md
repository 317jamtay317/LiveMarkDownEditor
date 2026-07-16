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
  strikethrough, inline code, Unordered and Ordered Lists (with nesting), links, autolinks, images,
  block quotes, fenced and indented code blocks, thematic breaks (horizontal rules), GFM tables
  (with column alignment), task-list items, and hard line breaks.

### INV-005 — Capture is idempotent over Round-Trips
- **Statement:** Once a Markdown Document has been Round-Tripped, Round-Tripping the result again
  produces identical source text. Capture converges — repeated Round-Trips never keep mutating the
  document (no whitespace drift, no escaping churn).
- **Enforced by:** Capture emitting normalised, canonical Markdown (`FlowDocumentToMarkdownCapturer`).
- **Tested by:** `WysiwygRoundTripTests.RoundTrip_IsIdempotent_INV005`.

### INV-006 — A Conflict never silently discards either side
- **Statement:** When an External Change to the Watched File **that changes content** (INV-026) is
  detected while the Editor Session has unsaved edits, a Conflict is raised and surfaced to the user
  for a decision. Neither the on-disk contents nor the unsaved edits are overwritten without an
  explicit choice.
- **Enforced by:** `ExternalChangeReconciler.Reconcile` and the Editor Session's
  `HandleExternalChangeAsync`, which raise a Conflict (rather than applying disk contents) when
  unsaved edits exist, and never overwrite without an explicit Keep/Reload choice.
- **Tested by:** `ExternalChangeReconcilerTests.Reconcile_WithUnsavedEdits_RaisesConflict_INV006`,
  `EditorSessionViewModelTests.ExternalChange_WithUnsavedEdits_RaisesConflict_AndKeepsEdits_INV006`.

### INV-007 — With no unsaved edits, an External Change reloads live
- **Statement:** When the Watched File changes in content (INV-026) and the Editor Session has **no**
  unsaved edits, the Markdown Document (and therefore the Visual Document) is updated to the new
  on-disk contents without prompting. This is the "live update by any user, including AI" behaviour.
- **Enforced by:** External-change reconciliation applying disk contents directly when the session
  is clean.
- **Tested by:** `ExternalChangeReconcilerTests.Reconcile_WithNoUnsavedEdits_ReloadsFromDisk_INV007`,
  `EditorSessionViewModelTests.ExternalChange_WhenSessionClean_ReloadsLive_INV007`.

### INV-008 — The Active Session tracks the open Editor Sessions
- **Statement:** A Workspace has exactly one Active Session while it holds any open Editor Sessions,
  and the Active Session is always one of those open Sessions. It opens with a single empty Editor
  Session. The Workspace **may become empty**: closing the last Tab leaves it with no open Editor
  Session and a `null` Active Session (it does **not** re-seed a fresh Tab), and the editing area
  shows the Empty-Workspace Placeholder. Opening or creating a document adds a Tab and makes it the
  Active Session again.
- **Enforced by:** `WorkspaceViewModel` — it creates an initial Editor Session in its constructor,
  keeps `ActiveSession` referencing an open Session (moving it to a neighbour when the Active Tab is
  closed), and sets `ActiveSession` to `null` when the last Tab is closed rather than re-seeding.
- **Tested by:** `WorkspaceViewModelTests.Constructor_StartsWithOneEmptyActiveSession_INV008`,
  `WorkspaceViewModelTests.Close_LastTab_LeavesWorkspaceEmpty_INV008`.

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

### INV-011 — Folding is a view-only operation
- **Statement:** Folding a Section hides its Section Body in the Visual Document but never changes
  the Markdown Document. Capturing the Visual Document yields identical Markdown source text whether
  or not any Section is Folded — a Fold hides a Section Body without removing it from the document's
  content. Only a Section Heading can be Folded; a non-heading block has no Section to Fold. Collapse
  All and Expand All are Folds applied across every Section and are equally view-only; the Editor
  Gutter (Line Numbers and Fold Toggles) is presentation-only and likewise never changes the
  Markdown Document.
- **Enforced by:** `MarkdownRichEditor` folding, which retains each Folded Section Body and Captures
  the full logical block sequence (visible blocks with Folded bodies spliced back in), so Capture is
  unaffected by fold state — including `CollapseAllFolds` / `ExpandAllFolds`. Section boundaries are
  computed by the pure `SectionMap`. The `EditorGutter` only reads the editor's blocks and fold state;
  it never mutates the document.
- **Tested by:** `MarkdownRichEditorTests.Fold_DoesNotChangeCapturedMarkdown_INV011`,
  `MarkdownRichEditorTests.CollapseAllFolds_DoesNotChangeCapturedMarkdown_INV011`,
  `SectionMapTests.FindBody_*`.

### INV-012 — The Outline and Navigation are view-only
- **Statement:** Building the Outline, showing or hiding the Navigation Panel, and Navigating to a
  Section Heading never change the Markdown Document. The Outline lists exactly the Active Session's
  Section Headings in document order — every one of them, including headings inside a Folded Section
  Body — and Navigating selects and scrolls to a Section Heading (Unfolding its enclosing Section
  first if needed) without altering the source text. Collapsing or Expanding an Outline Entry only
  hides or shows nested Outline Entries within the Navigation Panel — it changes neither the Markdown
  Document nor any Fold state in the Visual Document. Capturing the Visual Document yields identical
  Markdown source text before and after any Outline, Navigation, or Collapse/Expand action. (This is
  the Outline/Navigation counterpart of INV-011.)
- **Enforced by:** `MarkdownRichEditor.Outline` reading the full logical block sequence (so Folded
  headings are still listed) and `MarkdownRichEditor.Navigate` only Unfolding, selecting, and
  scrolling — never mutating the logical document. The `OutlinePanel` and its visibility toggle
  (`WorkspaceViewModel.IsNavigationPanelVisible`) only read the editor's Outline and drive Navigate;
  Collapse/Expand is computed by the pure `OutlineView` and applied to panel-only view state, never
  touching the editor.
- **Tested by:** `MarkdownRichEditorTests.Navigate_DoesNotChangeCapturedMarkdown_INV012`,
  `MarkdownRichEditorTests.Outline_ListsHeadingsInsideFoldedSections_INV012`,
  `OutlineViewTests.VisibleEntries_UnderCollapsedAncestor_AreHidden`.

### INV-013 — The Source Panel and the Visual Document share one Markdown Document
- **Statement:** The Source Panel and the Visual Document are two views of the *same* Markdown
  Document source text and never diverge. Editing the Source Panel edits the source directly, which
  Projects an updated Visual Document; editing the Visual Document Captures back into the source,
  which the Source Panel reflects. A change originating in one view is never echoed back to re-edit
  the other (no feedback loop), and after either edit both views represent identical Markdown source
  text.
- **Enforced by:** Both views binding two-way to the single canonical `EditorSessionViewModel.Markdown`
  source text, and the `MarkdownRichEditor` re-entrancy guard (`_isSynchronising` / `_lastCaptured`)
  that projects an incoming source change and captures an outgoing edit without the two directions
  echoing each other.
- **Tested by:** `MarkdownRichEditorTests.AssigningSource_ProjectsVisualDocument_AndVisualEdit_UpdatesSource_INV013`.

### INV-014 — Toggling the Source Panel is view-only
- **Statement:** Showing or hiding the Source Panel never changes the Markdown Document. The Source
  Panel is hidden until the user toggles it on, and toggling it alters neither the Active Session's
  source text nor any Fold, Outline, or Navigation state. (This is the Source Panel counterpart of
  INV-011/012.)
- **Enforced by:** `WorkspaceViewModel.IsSourcePanelVisible` and `ToggleSourcePanelCommand`, which
  drive only presentation state and never touch any Editor Session's Markdown.
- **Tested by:** `WorkspaceViewModelTests.Constructor_StartsWithSourcePanelHidden_INV014`,
  `WorkspaceViewModelTests.ToggleSourcePanel_TogglesVisibility_WithoutChangingDocument_INV014`.

### INV-015 — Scroll Sync is proportional and view-only
- **Statement:** While both the Visual Document and the Source Panel are shown, scrolling either view
  scrolls the other to the same **proportional** position — the same fraction of its scrollable
  height — in both directions. The mapping is proportional, not line-for-line: offset `0` maps to
  offset `0`, the maximum scroll of one maps to the maximum scroll of the other, and a view with no
  scrollable content (its content fits) leaves its partner unmoved rather than dividing by zero.
  Scroll Sync moves viewports only and never changes the Markdown Document.
- **Enforced by:** The pure `ProportionalScroll` calculation (a function of the source offset and the
  two scrollable heights, guarded against a zero scrollable height) and the `ScrollSync` behaviour,
  which wires the two views' scroll events to that calculation and only moves viewports (it never
  touches any Editor Session's Markdown).
- **Tested by:** `ProportionalScrollTests.*`.

### INV-016 — Find is view-only
- **Statement:** Finding a query, highlighting its Matches, moving the Current Match with Find Next
  / Find Previous, and opening or closing the Find Bar never change the Markdown Document. Find is a
  read-only overlay on the Visual Document: it computes Matches from a snapshot of the document's
  text and highlights, scrolls, and selects, but the Captured Markdown source text is identical
  before and after any Find. Replacing a Match (INV-022) is a separate operation and is not part of
  Find: locating a Match never edits, and only Replace and Replace All do.
- **Enforced by:** The pure `MatchFinder` (which computes ordered, non-overlapping Matches from a
  text snapshot with no reference to the document), the pure `MatchScanner` (which maps those
  Matches onto a Visual Document as ranges and only reads it), the `FindHighlightAdorner` (which
  only draws), and Find state (`FindQuery`, Current Match, Find Bar visibility) being
  presentation-only on the `MarkdownRichEditor` Control — none of it feeds back into Capture.
- **Tested by:** `MatchFinderTests.*`, `MatchScannerTests.*`,
  `MarkdownRichEditorTests.Find_DoesNotChangeCapturedMarkdown_INV016`.

### INV-017 — Code Shading is view-only
- **Statement:** Drawing Code Shading behind the Code Blocks and Code Spans of the Visual Document
  never changes the Markdown Document. Code Shading is a read-only overlay: it computes its Code
  Regions from the document and only paints behind them, so the Captured Markdown source text is
  identical before and after Code Shading is drawn or recoloured. Because the shading is an overlay
  rather than each code element's own background, recolouring it for a theme change repaints without
  re-formatting the Visual Document. (This is the Code-Shading counterpart of INV-016.)
- **Enforced by:** The pure `CodeShadingScanner` (which computes the ordered Code Regions from a
  Visual Document with no reference back to the source), the `CodeShadingAdorner` (which only draws),
  and the projector tagging code but assigning it no `Background` — the shade lives solely in the
  overlay. None of it feeds back into Capture.
- **Tested by:** `CodeShadingScannerTests.*`,
  `MarkdownToFlowDocumentProjectorTests.CodeElements_CarryNoBackground_SoShadingCannotReflow_INV017`,
  `MarkdownRichEditorTests.CodeShading_DoesNotChangeCapturedMarkdown_INV017`.

### INV-018 — A Formatting Action Captures to canonical Markdown
- **Statement:** Applying a Formatting Action (Toggle Strikethrough, Toggle Code, Set Heading Level,
  Insert Link, Insert Image, Toggle Block Quote, Insert Table, Add Row, Add Column,
  Toggle Unordered List, Toggle Ordered List, Toggle Task List) edits the Visual Document using the
  same tagged elements a Project produces, so the Captured source text is canonical Markdown:
  Round-Tripping it preserves its semantics (INV-004) and converges (INV-005). A Formatting Action
  never corrupts the document — after it runs, the Visual Document and the Markdown Document still
  describe the same content.
- **An emphasis delimiter hugs its text.** `**bold **` and `~~struck ~~` do not close in Markdown: a
  closing delimiter preceded by whitespace is not right-flanking, so it emits literal asterisks or
  tildes rather than emphasis. A user selecting a word by double-click or Ctrl+Shift+Right takes its
  trailing space with it, so Capture hoists whitespace surrounding an emphasised span **outside** the
  delimiters (`one ~~two~~ three`, never `one ~~two ~~three`). Whitespace alone carries no emphasis
  and is emitted bare. Without this the Markdown would say "literal tildes" where the Visual Document
  says "struck through" — the two would stop describing the same content.
- **Enforced by:** The Formatting Actions on `MarkdownRichEditor` composing the identical roles the
  Projector emits (`InlineSemantic.Code`, `InlineSemantic.Strikethrough`, `HeadingRole`,
  `BlockSemantic.Quote`, `LinkRole`, `ImageRole`, `CodeBlockRole`, `TableRole`, `TaskMarkerRole`)
  through the shared `CodeFormatting` / `HeadingFormatting` / `InlineFormatting` / `QuoteFormatting` /
  `LinkFormatting` / `TableEditing` / `ListFormatting` / `TaskMarkerEditing` helpers, so Capture
  treats user-applied formatting and loaded formatting uniformly. A List carries no role of its own —
  its kind rides on the WPF `List`'s own `MarkerStyle` — so the Projector composes a List through
  `ListFormatting.ApplyList` and a Task Marker through `TaskMarkerEditing.CreateMarker`, the same
  seams the Formatting Actions use (mirroring `CodeFormatting.ApplyCodeSpan` / `TableEditing.WrapCell`).
  The whitespace rule lives in the Capturer's `Emit`, so it holds for every emphasised span alike —
  including the bold and italic actions that predate it.
- **Tested by:** `MarkdownRichEditorToggleCodeTests.*_INV018`,
  `MarkdownRichEditorTableTests.*_INV018`, `MarkdownRichEditorListTests.*_INV018`,
  `MarkdownRichEditorHeadingTests.*_INV018`, `MarkdownRichEditorQuoteTests.*_INV018`,
  `MarkdownRichEditorStrikethroughTests.*_INV018` — in particular
  `ToggleStrikethrough_WithTrailingSpaceInSelection_KeepsTheSpaceOutsideTheDelimiters_INV018` and
  `ToggleBold_WithTrailingSpaceInSelection_KeepsTheSpaceOutsideTheDelimiters_INV018`.

### INV-019 — A Table stays rectangular
- **Statement:** Every row of a Table has exactly one cell per column, and the Table's per-column
  alignments list exactly one alignment per column. Insert Table creates such a Table; Add Row mints
  its new row at the Table's column count; Add Column extends every row — header and body — by one
  cell. No Table operation ever leaves a ragged Table.
- **Enforced by:** The `TableEditing` helper, which derives the new row's width from the Table's
  columns and inserts a cell into every row when adding a column, updating the `TableRole` alignments
  in the same operation.
- **Tested by:** `MarkdownRichEditorTableTests.AddRow_MatchesColumnCount_INV019`,
  `MarkdownRichEditorTableTests.AddColumn_ExtendsEveryRow_INV019`.

### INV-020 — A Startup Document opens in the one running Workspace
- **Statement:** Launching the editor with a Startup Document opens that file into the Workspace at
  startup. If the editor is already running, the Startup Document is forwarded to the running
  instance — whose Workspace opens it, or activates its existing Tab (INV-009) — and no second
  editor window appears (Single Instance).
- **Enforced by:** `StartupArguments` (parsing the Startup Document from the command line),
  `WorkspaceViewModel.OpenPathAsync` (the same dedupe-and-load path the file picker uses), and the
  `SingleInstanceGuard` (a named mutex plus named pipe that forwards the path to the first instance),
  wired together in the composition root (`Program`).
- **Tested by:** `StartupArgumentsTests.*`,
  `WorkspaceViewModelTests.OpenPath_WhenFileAlreadyOpen_ActivatesExistingTab_INV009_INV020`,
  `SingleInstanceGuardTests.*_INV020`.

### INV-021 — Viewing the Conflict Difference is view-only and deterministic
- **Statement:** Computing a Conflict Difference is a pure, deterministic function of the two sides —
  the same session text and disk text always yield the same Difference Lines — and every line of both
  sides is accounted for: the Unchanged and Session Only lines are exactly the session's lines in
  order, and the Unchanged and Disk Only lines are exactly the disk's lines in order. Showing or
  hiding the Conflict Difference (View Difference) never changes the Markdown Document, the Watched
  File, or the Conflict itself: both sides are retained, unchanged, until the user resolves the
  Conflict with an explicit choice (INV-006), and resolving it hides the difference.
- **Enforced by:** The pure static `ConflictDifference.Compute` (Domain — no I/O, no state), and the
  Editor Session's View Difference state (`ViewDifferenceCommand`, `IsDifferenceVisible`,
  `DifferenceLines`) driving only presentation, with the conflicting disk text retained until
  `ClearConflict`.
- **Tested by:** `ConflictDifferenceTests.Compute_GivenSameInputsTwice_YieldsIdenticalLines_INV021`,
  `ConflictDifferenceTests.Compute_AccountsForEveryLineOfBothSides_INV021`,
  `EditorSessionViewModelTests.ViewDifference_ShowsDifference_WithoutChangingMarkdownOrConflict_INV021`.

### INV-022 — Replace is a real edit that Captures to canonical Markdown
- **Statement:** Replace swaps the Current Match for the Replacement; Replace All swaps every Match
  for it. Both are real edits (the counterpart of Find's INV-016 view-only guarantee), and both edit
  the Visual Document, so the result Captures as canonical Markdown: Round-Tripping it preserves its
  semantics (INV-004) and converges (INV-005). Three rules bound the edit:
  - **The Replacement is inserted verbatim** — exactly as typed, never adapting its case to the Match
    it replaces (a Match is found case-insensitively, but a Replacement is not rewritten to suit it).
    It inherits the Match's formatting when the Match *has* one: replacing a word inside bold text
    leaves it bold. A Match spanning a formatting boundary has no single formatting to inherit, so
    its Replacement is plain. An empty Replacement deletes the Match.
  - **Replace All spans the whole Markdown Document.** Because Find searches only the visible Visual
    Document, Replace All Unfolds every Folded Section first (INV-011, view-only) and re-finds, so an
    occurrence hidden inside a Folded Section Body is replaced rather than silently left behind.
  - **Replace All terminates.** It replaces exactly the Matches present when it is invoked, so a
    Replacement that contains the query does not re-match and cannot cascade.
- **Enforced by:** `MatchReplacer` (which swaps a Match's span for the Replacement through the same
  `TextRange.Text` mechanism a Spelling Suggestion uses — WPF carries the surrounding formatting into
  the new text, and flattens where the span had no single formatting), and `MarkdownRichEditor`'s
  `ReplaceCurrentMatch` / `ReplaceAllMatches`, which Unfold before Replacing All and iterate a
  snapshot of the Match ranges taken before the first edit. The edit then flows through the same
  `OnTextChanged` → Capture path as any other edit, which is what makes the Captured source
  canonical. Replace All's availability (`CanReplaceAll`) turns on the query alone and never on the
  Match count: Find cannot see into a Folded Section, so gating the command on the count would
  disable it in precisely the case this invariant exists to cover.
- **Tested by:** `MarkdownRichEditorReplaceTests.*_INV022`, in particular
  `ReplaceAll_CapturesCanonicalMarkdown_ThatRoundTrips_INV022` (anti-corruption),
  `ReplaceAll_UnfoldsFoldedSections_SoNoMatchIsMissed_INV022`, and
  `ReplaceAll_WhenReplacementContainsQuery_ReplacesOnlyTheOriginalMatches_INV022` (termination).

### INV-023 — A List Toggle preserves its List Items' content
- **Statement:** Toggle Unordered List, Toggle Ordered List, and Toggle Task List change a List's
  *kind* or its Task Markers — never the content of its List Items. Four rules bound them:
  - **Content survives every toggle.** Turning paragraphs into a List, a List back into paragraphs,
    or an Unordered List into an Ordered one preserves each item's text, its inline formatting, and
    the order of the items. One paragraph becomes one List Item, and one List Item becomes one
    paragraph.
  - **The two List kinds toggle between each other, never off through each other.** Toggle Ordered
    List applied to an Unordered List makes it Ordered (and vice versa); only the *same* kind's
    toggle turns a List back into paragraphs. So a List is never silently destroyed by reaching for
    the other kind.
  - **A Task Marker exists only on a List Item.** Applied outside a List, Toggle Task List makes the
    selected paragraphs an Unordered List first and then marks them — it never refuses the action.
    Turning a Task List back into paragraphs removes its Task Markers with it: no Task Marker can
    outlive the List Item that carries it.
  - **Toggle Task List is all-or-nothing over the selection.** It removes the Task Markers only when
    every selected List Item already carries one; otherwise it gives an unchecked Task Marker to
    those that lack one, so a partly-marked selection converges on marked rather than flip-flopping.
  - **A Task Marker's checkbox is its List Item's marker.** An Unordered List whose every item is a
    task item shows no bullet — a bullet beside a checkbox is one marker too many. It regains its
    bullets the moment any item is not a task item, because WPF gives a List one marker for all of
    its items and the unmarked items would otherwise be left with no marker at all. An Ordered Task
    List keeps its numbers. This is presentation only: it never changes the Captured source text.
  - **A Task List continues across a paragraph break.** Breaking the line in a task item gives the
    new List Item its own unchecked Task Marker, the way a bullet or a number carries to the next
    item — the new item is a task item, so the List does not regain its bullets. A Task Marker's
    checked state never carries: the new item always starts unchecked.
  - **A Task Marker's Run never swallows the item's text.** The marker is ordinary editable text and
    the caret legitimately sits inside its Run — a new task item's marker is its only inline, so WPF
    normalises the caret into it and the first thing typed lands there. Capture emits the marker from
    its role, and must emit whatever that Run holds beyond the checkbox glyph as the item's text.
    Otherwise a label the user can see on screen would never reach the Markdown Document.
- **Enforced by:** The `ListFormatting` helper, which composes its List through the same
  `ApplyList` seam the Projector uses (INV-018) and **moves each item's existing paragraph** into the
  List (and back out again) rather than re-creating it from text — so inline formatting cannot be
  flattened by a toggle. `ListFormatting.RefreshTaskMarkerStyle` is the one place the bullet rule
  lives, applied by the Projector and by every List Formatting Action alike; Capture tells an Ordered
  List from an Unordered one by `MarkerStyle == Decimal`, and `None` is not `Decimal`, so dropping the
  bullet cannot change the captured marker. `ListFormatting.TryContinueTaskList` leaves the paragraph
  break itself to WPF's `EditingCommands.EnterParagraphBreak` — which carries inline formatting across
  the break correctly — and `MarkContinuedTaskItem` then supplies the one thing WPF cannot know about,
  the new item's Task Marker, in the same `BeginChange` unit so one undo takes the whole new item back.
- **Tested by:** `MarkdownRichEditorListTests.*_INV023`. The paragraph break runs through a WPF editing
  command that needs a focused editor and so does nothing in a headless test; the tests cover the rule
  that marks the item the break creates, and Enter itself is verified by driving the real app.

### INV-024 — Toggle Task Marker flips only that Task Marker's state
- **Statement:** Clicking a Task Marker's checkbox toggles it between unchecked (`[ ]`) and checked
  (`[x]`) and changes nothing else: its List Item's content, its List's kind and numbering, and every
  other List Item are left exactly as they were. It is a real edit — the Captured source text differs
  in precisely that one marker — and it Captures as canonical Markdown, so Round-Tripping the result
  preserves its semantics (INV-004) and converges (INV-005). Toggling is reachable **only** by
  clicking a Task Marker itself: a click anywhere else in the Visual Document places the caret as it
  always has and never toggles a marker. Because a Task Marker's checked state is carried by its role
  rather than by the glyph the user sees, a toggle updates both together — the glyph and the role can
  never disagree about whether the task is checked.
- **Enforced by:** `TaskMarkerEditing`, which resolves the clicked position to a `TaskMarkerRole`-tagged
  `Run` (returning without an edit when the click is not on one) and replaces both that `Run`'s `Tag`
  and its glyph in a single `BeginChange`/`EndChange` unit, and `MarkdownRichEditor.OnPreviewMouseLeftButtonDown`,
  which only forwards the click and lets it fall through to normal caret placement otherwise.
- **Tested by:** `MarkdownRichEditorTaskMarkerTests.*_INV024`.

### INV-025 — A Conflict Difference compares Canonical Markdown
- **Statement:** The two sides a Conflict Difference compares are the **Canonical Markdown** of the
  Editor Session's unsaved source text and the Canonical Markdown of the Watched File's conflicting
  on-disk contents — each side Round-Tripped before it is compared. A difference of Markdown syntax
  style alone therefore never appears as a Difference Line: because Capture emits Canonical Markdown
  (INV-005), an edited Editor Session holds canonical source text, and a Watched File authored in
  another style would otherwise differ on every restyled line even where the two sides say the same
  thing. Every Difference Line shown is a difference of content, so View Difference answers the
  question a Conflict actually poses (INV-006) — what did the other writer change, and what would I
  lose — rather than burying it in formatting churn.
- **Consequence (accepted):** The Conflict Difference is a comparison of meaning, not of bytes. A line
  shown as Unchanged may still differ from the Watched File's own text, and resolving the Conflict
  with Keep My Edits then saving rewrites those lines to Canonical Markdown. The Conflict Difference
  does not claim to predict a save's byte-level output.
- **Enforced by:** `EditorSessionViewModel.RefreshDifference`, which Round-Trips both sides through
  its `IMarkdownRoundTrip` port before calling `ConflictDifference.Compute`; the `FlowDocumentRoundTrip`
  adapter realises the port as a Project immediately followed by a Capture. `Compute` itself stays pure
  and unaware — it compares whatever two sides it is given (INV-021).
- **Tested by:** `EditorSessionViewModelTests.ViewDifference_StillShowsARealChange_BesideChurn_INV025`,
  `FlowDocumentRoundTripTests.*_INV025`.
- **Note:** A Conflict Difference showing *every* line Unchanged is unreachable — INV-026 ignores a
  bytes-only External Change rather than raising a Conflict over it. Churn is therefore only ever
  seen beside a real change, which is what the test above pins.

### INV-026 — An External Change that changes no content is ignored
- **Statement:** An External Change whose new on-disk contents share the Editor Session's Canonical
  Markdown changes bytes but not content, and is acted on by neither INV-006 nor INV-007: no Conflict
  is raised, and no live reload occurs. This covers the Editor Session's own save (where the disk
  contents come to equal the session's source text outright) and any other writer restyling the
  Watched File — setext headings rewritten as ATX, `_` emphasis as `*`, blank-line spacing changed.
  It follows from INV-025: because a Conflict Difference compares Canonical Markdown, a Conflict
  raised by a bytes-only change would show every line Unchanged, demanding the user resolve a
  difference they cannot see. The rule is reached often, not rarely: Capture rewrites the *whole*
  Markdown Document as Canonical Markdown, so one keystroke in a Watched File authored in another
  style leaves an Editor Session that agrees with the file in content while differing from it in
  bytes on every restyled line.
- **Consequence (accepted):** The externally written bytes are not adopted — the Editor Session keeps
  its own source text, and a later save rewrites the Watched File as Canonical Markdown (INV-005).
  No content is discarded, so INV-006 holds: what is dropped is a styling of the same content, which
  the Editor Session would overwrite on its next save regardless.
- **Enforced by:** `EditorSessionViewModel.HandleExternalChangeAsync`, which returns before
  reconciling when the two sides share Canonical Markdown, Round-Tripping each through its
  `IMarkdownRoundTrip` port.
- **Tested by:** `EditorSessionViewModelTests.ExternalChange_ThatMatchesSession_IsIgnored_INV026`,
  `EditorSessionViewModelTests.ExternalChange_ThatOnlyRestylesTheWatchedFile_WhenSessionClean_IsIgnored_INV026`,
  `EditorSessionViewModelTests.ExternalChange_ThatOnlyRestylesTheWatchedFile_WithUnsavedEdits_RaisesNoConflict_INV026`.

### INV-027 — Set Heading Level changes a block's level, never its content
- **Statement:** Set Heading Level changes only the Heading Level of the block at the caret. Four rules
  bound it:
  - **Content survives.** Making a paragraph a Heading, changing a Heading's level, and turning a
    Heading back into a paragraph all preserve the block's text, its inline formatting, and its
    position in the document. One block goes in and the same block, relevelled, comes out.
  - **It sets, it does not toggle.** Choosing the level a Heading already has leaves it a Heading of
    that level, so the action is idempotent. Only **Paragraph** clears a Heading — a level is a value
    the user picks, so reaching for a level can never silently destroy the Heading.
  - **A Heading Level is always 1–6.** No other level is reachable: the Heading Level Picker offers
    exactly six, and Set Heading Level refuses any level outside 1–6 (Paragraph aside) rather than
    writing one. `#` repeats once per level, and a seventh `#` is not a heading in Markdown at all.
  - **A Heading is sized, never weighted.** The Heading a Set Heading Level produces is styled by the
    same seam the Projector uses, so it is distinguished by size alone — a bold weight would make
    Capture read the Heading's text as inline-bold and emit `# **text**` (INV-018).
- **Enforced by:** The `HeadingFormatting` helper, which **relevels the caret's existing paragraph in
  place** — setting or clearing its `HeadingRole` and restyling it — rather than re-creating it from
  text, so inline formatting cannot be flattened by a change of level. `HeadingFormatting.ApplyHeading`
  is the one place a Heading's styling lives, applied by the Projector and by Set Heading Level alike
  (mirroring `ListFormatting.ApplyList`), and `HeadingFormatting.SetLevel` ignoring a level outside
  the Paragraph-or-1–6 range.
- **Tested by:** `MarkdownRichEditorHeadingTests.*_INV027`, in particular
  `SetHeadingLevel_PreservesInlineFormatting_INV027`,
  `SetHeadingLevel_ToTheSameLevel_LeavesItAHeading_INV027`, and
  `SetHeadingLevel_GivenLevelOutsideOneToSix_LeavesTheDocumentUnchanged_INV027`.

### INV-028 — Toggle Block Quote quotes whole blocks, and preserves them
- **Statement:** Toggle Block Quote turns the blocks the selection touches into a Block Quote, and
  turns a Block Quote's blocks back into plain blocks. Three rules bound it:
  - **It quotes whole blocks, never part of one.** A Block Quote is captured as a `> ` prefix on
    every line, so quoting half a paragraph cannot be expressed in Markdown. A selection that starts
    or ends mid-block quotes that whole block, and a caret alone quotes the block it sits in.
  - **Content survives both directions.** Quoting blocks and unquoting them preserves each block's
    text, its inline formatting, its kind (a Heading stays a Heading, a List stays a List), and the
    order of the blocks. The blocks are **moved** into the Block Quote and back out again, never
    re-created from their text.
  - **Unquoting restores the blocks at top level.** A Block Quote turned off leaves its blocks in the
    document in their original order, in its place — not merged into a neighbour, and not dropped.
- **Enforced by:** The `QuoteFormatting` helper, which moves the selected top-level blocks into a
  `Section` composed through the same `ApplyQuote` seam the Projector uses (INV-018) — so a loaded
  Block Quote and a user-made one are identical to Capture — and moves them back out on the reverse
  toggle, in both cases relocating the existing blocks rather than rebuilding them.
- **Tested by:** `MarkdownRichEditorQuoteTests.*_INV028`, in particular
  `ToggleBlockQuote_WithPartialSelection_QuotesTheWholeBlock_INV028`,
  `ToggleBlockQuote_PreservesInlineFormatting_INV028`, and
  `ToggleBlockQuote_OnAQuote_RestoresItsBlocksAtTopLevel_INV028`.

### INV-029 — Toggle Strikethrough is symmetric over where the Strikethrough came from
- **Statement:** Toggle Strikethrough strikes the selection through, or restores struck-through prose
  to plain text. It removes a Strikethrough the Projector loaded exactly as readily as one a previous
  toggle applied: the two are the same thing to the user, so the action cannot be able to undo only
  its own work. Its content survives both directions — striking text through and restoring it
  preserves the text and its other inline formatting (bold stays bold).
- **Rationale:** Unlike bold and italic — which ride on `FontWeight` / `FontStyle`, inherited
  properties an inner Run can override — a Strikethrough rides on `TextDecorations`, which Capture
  reads by walking a Run's **ancestors**. So clearing the decoration on the selected Run alone would
  leave an enclosing struck Span still striking it, and the text would still Capture as `~~text~~`
  while looking plain. The Strikethrough must be removed where it lives.
- **Enforced by:** The `StrikethroughFormatting` helper, which finds the struck Spans the selection
  touches — by the same `InlineSemantic.Strikethrough` role and `TextDecorations` that Capture reads
  — and clears them, and which otherwise wraps the selection in a Span composed through the same
  `ApplyStrikethrough` seam the Projector uses (INV-018), mirroring `CodeFormatting`'s treatment of a
  Code Span.
- **Tested by:** `MarkdownRichEditorStrikethroughTests.*_INV029`, in particular
  `ToggleStrikethrough_OnALoadedStrikethrough_RemovesIt_INV029` and
  `ToggleStrikethrough_PreservesOtherInlineFormatting_INV029`.

<!--
Add new invariants above using the next INV-### number. Never reuse a retired number.
Every invariant MUST have at least one corresponding test before it is considered done.
-->
